# -*- coding: utf-8 -*-
import os
import sys
import re
import time
import random
import logging
from collections import Counter
from flask import Flask, request, jsonify
import requests
from bs4 import BeautifulSoup
import cv2
import numpy as np
import ddddocr

logging.basicConfig(level=logging.INFO, format="[%(asctime)s] %(message)s")

EDEVLET_URL = "https://www.turkiye.gov.tr/imei-sorgulama"
EDEVLET_SUBMIT_URL = "https://www.turkiye.gov.tr/imei-sorgulama?submit"

ocr = None
try:
    ocr = ddddocr.DdddOcr(show_ad=False)
    logging.info("Yerel ddddocr ONNX OCR modeli başarıyla yüklendi.")
except Exception as e:
    logging.error(f"OCR yükleme hatası: {e}")

USER_AGENTS = [
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0"
]

def solve_captcha_multipass(img_bytes):
    """
    e-Devlet Captcha Çözücü:
    e-Devlet güvenlik kodları daima 5 haneli (küçük/büyük harf ve rakam) olup,
    üzerinde 1px incelikte mavi/pembe parazit çizgiler bulunur.
    OpenCV çoklu filtreleme (Median blur, Morfolojik Açma, Kontrast germe, Otsu)
    uygulanarak gürültü çizgileri temizlenir ve en yüksek güvenilirlikteki 5 haneli
    tahmin çoğunluk oylaması (majority vote) ile seçilir.
    """
    if not ocr or not img_bytes:
        return ""
    try:
        nparr = np.frombuffer(img_bytes, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if img is None:
            raw_pred = ocr.classification(img_bytes).strip()
            return "".join(c for c in raw_pred if c.isalnum())

        candidates = []

        # 0. Ham Görsel
        c0 = ocr.classification(img_bytes).strip()
        if c0:
            candidates.append(c0)

        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

        # 1. Kontrast Genişletme + Otsu Eşikleme
        norm = cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX)
        _, th1 = cv2.threshold(norm, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        _, b1 = cv2.imencode(".png", th1)
        c1 = ocr.classification(b1.tobytes()).strip()
        if c1:
            candidates.append(c1)

        # 2. Median Blur (3x3) + Otsu (1px ince parazit çizgileri temizler)
        med = cv2.medianBlur(gray, 3)
        _, th2 = cv2.threshold(med, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        _, b2 = cv2.imencode(".png", th2)
        c2 = ocr.classification(b2.tobytes()).strip()
        if c2:
            candidates.append(c2)

        # 3. Morfolojik Açma (Morph Open 2x2) + Otsu
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (2, 2))
        morph = cv2.morphologyEx(gray, cv2.MORPH_OPEN, kernel)
        _, th3 = cv2.threshold(morph, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        _, b3 = cv2.imencode(".png", th3)
        c3 = ocr.classification(b3.tobytes()).strip()
        if c3:
            candidates.append(c3)

        # 4. Bilateral Filtreleme + Otsu
        bi = cv2.bilateralFilter(gray, 5, 50, 50)
        _, th4 = cv2.threshold(bi, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        _, b4 = cv2.imencode(".png", th4)
        c4 = ocr.classification(b4.tobytes()).strip()
        if c4:
            candidates.append(c4)

        # 5. Gaussian Blur (3x3) + Otsu
        gauss = cv2.GaussianBlur(gray, (3, 3), 0)
        _, th5 = cv2.threshold(gauss, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        _, b5 = cv2.imencode(".png", th5)
        c5 = ocr.classification(b5.tobytes()).strip()
        if c5:
            candidates.append(c5)

        # Sadece alfanümerik karakterleri koru
        cleaned_list = []
        for c in candidates:
            cl = "".join(ch for ch in c if ch.isalnum())
            if cl:
                cleaned_list.append(cl)

        # e-Devlet captchaları tam 5 karakterdir. 5 karakter olanlara mutlak öncelik ver
        len5 = [c for c in cleaned_list if len(c) == 5]
        if len5:
            counts = Counter(len5)
            winner = counts.most_common(1)[0][0]
            logging.info(f"Hibrit OCR (5 Hane Çoğunluk Seçimi): {winner} (Adaylar: {candidates})")
            return winner

        if cleaned_list:
            counts = Counter(cleaned_list)
            winner = counts.most_common(1)[0][0]
            logging.info(f"Hibrit OCR (Yedek Seçim): {winner} (Adaylar: {candidates})")
            return winner

        return ""
    except Exception as ex:
        logging.error(f"Gelişmiş Captcha Çözme Hatası: {ex}")
        return ""

class BTKQueryEngine:
    def __init__(self, max_retries=6):
        self.max_retries = max_retries

    def _make_session(self):
        sess = requests.Session()
        ua = random.choice(USER_AGENTS)
        sess.headers.update({
            "User-Agent": ua,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
            "Accept-Language": "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7",
            "Accept-Encoding": "gzip, deflate, br",
            "Connection": "keep-alive",
            "Sec-Ch-Ua": '"Chromium";v="128", "Not;A=Brand";v="24", "Google Chrome";v="128"',
            "Sec-Ch-Ua-Mobile": "?0",
            "Sec-Ch-Ua-Platform": '"Windows"',
            "Sec-Fetch-Dest": "document",
            "Sec-Fetch-Mode": "navigate",
            "Sec-Fetch-Site": "same-origin",
            "Sec-Fetch-User": "?1",
            "Upgrade-Insecure-Requests": "1"
        })
        return sess

    def _get_and_solve_captcha(self, sess, captcha_src):
        """Captcha görselini indirir; eğer 5 hane çıkmazsa 1 kez yenileyip en iyisini döner"""
        if captcha_src.startswith("/"):
            captcha_src = "https://www.turkiye.gov.tr" + captcha_src

        headers = {"Referer": EDEVLET_URL}
        try:
            r_img = sess.get(captcha_src, headers=headers, timeout=10)
            if r_img.status_code == 200:
                code = solve_captcha_multipass(r_img.content)
                if len(code) == 5:
                    return code
                # Eğer 5 hane çıkmadıysa, captcha'yı bir kez yenile (fresh request)
                time.sleep(0.3)
                r_img2 = sess.get(captcha_src, headers=headers, timeout=10)
                if r_img2.status_code == 200:
                    code2 = solve_captcha_multipass(r_img2.content)
                    if len(code2) == 5:
                        return code2
                    if len(code2) > len(code):
                        return code2
                return code
        except Exception as e:
            logging.error(f"Captcha indirme/çözme hatası: {e}")
        return ""

    def query_imei(self, imei_input):
        clean_imei = re.sub(r"\D", "", str(imei_input))
        if len(clean_imei) != 15:
            return {
                "success": False,
                "category": "HATALI",
                "durum": f"Geçersiz IMEI formatı (15 hane olmalıdır, girilen: {len(clean_imei)})",
                "kaynak": "Doğrulama",
                "sorgu_tarihi": time.strftime("%d.%m.%Y %H:%M:%S"),
                "marka_model": "-"
            }

        logging.info(f"e-Devlet IMEI Canlı Sorgusu Başlatılıyor: {clean_imei}")

        for attempt in range(self.max_retries):
            try:
                sess = self._make_session()

                # 1. Sayfayı aç ve form öğelerini al
                r_get = sess.get(EDEVLET_URL, timeout=15)
                if r_get.status_code != 200:
                    logging.warning(f"GET {r_get.status_code} döndü. 2s beklenip tekrar deneniyor ({attempt + 1}/{self.max_retries})...")
                    time.sleep(2)
                    continue

                soup = BeautifulSoup(r_get.text, "html.parser")
                token_el = soup.find("input", {"name": "token"})
                token_val = token_el.get("value", "") if token_el else ""

                captcha_input = soup.find("input", {"name": re.compile(r"chkKayitNo|captcha|guvenlik", re.I)})
                captcha_img = soup.find("img", {"src": re.compile(r"captcha|guvenlik", re.I)})

                captcha_field_name = captcha_input.get("name", "chkKayitNo") if captcha_input else "chkKayitNo"
                captcha_code = ""

                if captcha_img:
                    captcha_code = self._get_and_solve_captcha(sess, captcha_img.get("src", ""))
                    if not captcha_code:
                        logging.warning(f"Captcha ilk aşamada okunamadı. Yeni oturum deneniyor ({attempt + 1}/{self.max_retries})...")
                        time.sleep(1)
                        continue

                # Oturum İçi Tekrar Döngüsü (Captcha hatalı olursa yeni token ve captcha ile aynı oturumda 3 kez tekrar dener)
                in_session_tries = 0
                max_in_session = 3

                while in_session_tries < max_in_session:
                    in_session_tries += 1

                    # POST Gönderimi
                    form_data = {
                        "txtImei": clean_imei,
                        "submit": "Sorgula"
                    }
                    if token_val:
                        form_data["token"] = token_val
                    if captcha_code:
                        form_data[captcha_field_name] = captcha_code

                    post_headers = {
                        "Origin": "https://www.turkiye.gov.tr",
                        "Referer": EDEVLET_URL,
                        "Content-Type": "application/x-www-form-urlencoded"
                    }

                    r_post = sess.post(EDEVLET_SUBMIT_URL, data=form_data, headers=post_headers, timeout=20)
                    if r_post.status_code != 200:
                        time.sleep(1.5)
                        break

                    resp_text = r_post.text
                    resp_lower = resp_text.lower()

                    # 1. 'Teknik Aksaklık' Kontrolü (Rate-limiting / Sunucu Blokajı)
                    if any(phrase in resp_lower for phrase in [
                        "teknik aksaklık",
                        "teknik aksaklik",
                        "işleminiz tamamlanamadı",
                        "isleminiz tamamlanamadi"
                    ]):
                        logging.warning(f"e-Devlet 'Teknik Aksaklık' (Rate-Limit) bildirdi! Oturum sıfırlanıyor ve 4 saniye bekleniyor... ({attempt + 1}/{self.max_retries})")
                        time.sleep(4.0)
                        break # İç döngüyü kır, dış döngüde taze oturumla dene

                    soup_res = BeautifulSoup(resp_text, "html.parser")
                    dt_list = soup_res.find_all("dt")
                    dd_list = soup_res.find_all("dd")

                    # 2. Başarılı Sonuç Tablosu Geldi mi?
                    if dt_list and dd_list:
                        res_dict = {}
                        for dt, dd in zip(dt_list, dd_list):
                            k = dt.get_text(strip=True).rstrip(":")
                            v = dd.get_text(strip=True)
                            res_dict[k] = v

                        durum = res_dict.get("Durum", "")
                        marka_model = res_dict.get("Marka/Model", "-")
                        kaynak = res_dict.get("Kaynak", "e-Devlet / BTK")

                        if durum:
                            d_lower = durum.lower()
                            cat = "KAYIT_DISI"

                            if any(w in d_lower for w in ["kayıtlı", "kayitli", "ithalat", "yolcu", "imalat"]):
                                cat = "KAYITLI"
                            elif any(w in d_lower for w in ["kopyalan", "klon", "eşleşmeyen", "eslesmeyen"]):
                                cat = "KOPYALANMIS"
                            elif any(w in d_lower for w in ["savcı", "adli", "çalıntı", "calinti"]):
                                cat = "SAVCILIK_KAPATILMIS"
                            elif any(w in d_lower for w in ["kullanılmadığı", "kapatılan", "1 yıl", "7 yıl"]):
                                cat = "KULLANILMAYAN_KAPATILMIS"

                            logging.info(f"Sorgu Başarılı: {clean_imei} -> {durum} [{cat}]")
                            return {
                                "success": True,
                                "imei": clean_imei,
                                "category": cat,
                                "durum": durum,
                                "marka_model": marka_model if marka_model else "-",
                                "kaynak": kaynak,
                                "sorgu_tarihi": time.strftime("%d.%m.%Y %H:%M:%S")
                            }

                    # 3. 'Hatalı Alan / Güvenlik Kodu' Hatası (Captcha Yanlış Girişi)
                    is_captcha_fail = any(phrase in resp_lower for phrase in [
                        "hatalı alanları düzelterek",
                        "hatali alanlari duzelterek",
                        "güvenlik kodunu hatalı",
                        "guvenlik kodunu hatali",
                        "güvenlik kodu",
                        "yeniden deneyiniz"
                    ])

                    if is_captcha_fail:
                        # Oturumu kapatmadan yeni token ve yeni captcha görselini alıp tekrar dene!
                        new_token_el = soup_res.find("input", {"name": "token"})
                        new_captcha_img = soup_res.find("img", {"src": re.compile(r"captcha|guvenlik", re.I)})
                        new_captcha_input = soup_res.find("input", {"name": re.compile(r"chkKayitNo|captcha|guvenlik", re.I)})

                        if new_token_el:
                            token_val = new_token_el.get("value", "")
                        if new_captcha_input:
                            captcha_field_name = new_captcha_input.get("name", "chkKayitNo")

                        if new_captcha_img:
                            logging.warning(f"Captcha eşleşmedi ('Hatalı alan'). Yeni captcha çözülüyor (Oturum içi deneme {in_session_tries}/{max_in_session})...")
                            time.sleep(1.2)
                            captcha_code = self._get_and_solve_captcha(sess, new_captcha_img.get("src", ""))
                            continue
                        else:
                            # Captcha görseli yoksa sayfayı tazelemek gerek
                            break

                    # Sonuç ya da bilinen hata yoksa alert div kutusunu tara
                    alert_el = soup_res.find("div", class_=re.compile(r"warning|error|info|result", re.I))
                    if alert_el:
                        msg = alert_el.get_text(strip=True)
                        if any(w in msg.lower() for w in ["hatalı alan", "yeniden deneyin", "güvenlik kodu"]):
                            logging.warning(f"Alert kutusunda captcha hatası tespit edildi. Tekrar çözülüyor...")
                            time.sleep(1.2)
                            continue
                        # Gerçek durum metni olabilir
                        if msg and len(msg) > 5:
                            logging.info(f"Alert kutusu durumu: {msg}")
                            return {
                                "success": True,
                                "imei": clean_imei,
                                "category": "KAYIT_DISI",
                                "durum": msg,
                                "marka_model": "-",
                                "kaynak": "BTK / e-Devlet",
                                "sorgu_tarihi": time.strftime("%d.%m.%Y %H:%M:%S")
                            }

                    time.sleep(1.2)

                # While döngüsü bitti ama sonuç alınamadıysa oturumu kapatıp yeniden başla
                time.sleep(1.5)

            except Exception as e:
                logging.error(f"Sorgu hatası (Deneme {attempt + 1}/{self.max_retries}): {e}")
                time.sleep(2.5)

        return {
            "success": False,
            "imei": clean_imei,
            "category": "HATALI",
            "durum": "Sorgu tamamlanamadı (e-Devlet yanıt vermedi)",
            "marka_model": "-",
            "kaynak": "BTK / e-Devlet",
            "sorgu_tarihi": time.strftime("%d.%m.%Y %H:%M:%S")
        }

engine = BTKQueryEngine()
app = Flask(__name__)

@app.route("/api/query/single", methods=["GET"])
def api_query_single():
    imei = request.args.get("imei", "")
    res = engine.query_imei(imei)
    return jsonify(res)

@app.route("/api/health", methods=["GET"])
def api_health():
    return jsonify({"status": "ok", "ocr_loaded": ocr is not None})

if __name__ == "__main__":
    port = 5892
    logging.info(f"RemoFix BTK & Hibrit Captcha Motoru {port} portunda başlatılıyor...")
    app.run(host="127.0.0.1", port=port, debug=False, use_reloader=False)
