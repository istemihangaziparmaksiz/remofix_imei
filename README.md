# Remofix IMEI

BTK / e-Devlet servisleri üzerinden tekli ve toplu IMEI sorgulaması yapmak, dönen sonuçları yerel kasa içinde arşivlemek ve takip etmek için geliştirilmiş bağımsız bir Windows masaüstü aracıdır.

Saha teknisyenleri ve telefon servisleri için tekrarlayan sorgulama süreçlerini hızlandırmak ve IMEI kayıtlarını harici bir sunucuya veya veritabanına ihtiyaç duymadan güvenle saklamak amacıyla tasarlanmıştır.

---

### Özellikler

- **Tekli & Toplu Sorgulama:**
  - 14 hane girildiğinde Luhn algoritmasıyla 15. kontrol basamağını (Check Digit) anında otomatik hesaplar.
  - Yüzlerce IMEI numarasını tek seferde liste halinde kuyruğa alıp sorgulayabilir.
  - **Hedef Bulunca Otomatik Durdurma:** Toplu sorgulamada belirlenen sayıda "Kayıtlı" IMEI bulunduğunda sorguyu otomatik durdurup kasaya kaydetme.
  - **İşlemi Duraklat / Durdur:** Toplu sorgulama veya kasa kontrolü devam ederken işlemi istenilen an duraklatabilme (`Duraklat / Devam Et`) veya tamamen iptal edebilme (`Durdur`).
- **Dahili Doğrulama Motoru:** Arka planda çalışan optimize edilmiş captcha motoru sayesinde doğrulama kodlarını otomatik çözer, e-Devlet kapısından marka/model ve kayıt durumunu çeker.
- **Yerel ve Şifreli IMEI Kasası:**
  - Herhangi bir bulut servisine, SQL veya uzak sunucuya bağlanmaz; tamamen yereldir ve doğrudan taşınabilir `.exe` içinde şifrelenerek saklanır.
  - Veriler AES-256-CBC ve HMAC-SHA256 ile şifrelenir, dışarıdan kurcalamaya karşı korumalıdır.
  - **Sorgusuz Manuel IMEI Ekleme:** Kayıtlı olduğundan emin olunan IMEI numaralarını e-Devlet sorgusu yapmadan doğrudan IMEI, Marka/Model ve Not bilgileriyle kasaya hızlıca ekleyebilme.
- **Çoklu Seçim & Kasa Yeniden Kontrol Sistemi:**
  - IMEI numaralarının solundaki seçim noktalarıyla tekli veya toplu seçim yapabilme, seçilenleri tek tıkla kopyalama, silme veya yalnızca seçili IMEI'leri yeniden sorgulama.
  - Hem **Kullanılabilir** hem de **Kullanılanlar** sekmesindeki tüm (veya seçili) IMEI numaralarını tek tıkla yeniden kontrol ederek klona düşen veya kapanan cihazları tespit etme.
  - İşlem sonunda kaç adet kayıtlı, klonlanmış, kayıt dışı veya kapatılmış IMEI olduğunu özetler; isteğe bağlı olarak kayıt dışı/klonlanmış olanları tek tıkla kasadan temizler.
- **Çift SIM (SIM 1 + SIM 2) Eşleştirme & Detaylı Kullanım Kaydı:**
  - Çift hatlı cihazlar için kasadan 2 adet IMEI seçip tek seferde **Çift SIM Olarak Kullanıldı** işaretleme; her iki IMEI'yi birbirine bağlayarak aynı müşteri ve cihaz kaydıyla kullanılanlara taşıma.
  - Çift SIM olarak kullanılan IMEI çiftlerinden biri tekrar kullanılabilir kasasına geri alındığında eşleşen diğer IMEI'yi de otomatik olarak kullanılabilir kasasına geri taşıma.
  - Bir IMEI'yi kullanıldı olarak işaretlerken **Müşteri Adı / İletişim**, **İşlem Yapılan Cihaz** ve **Açıklama / Not** bilgilerini ayrı alanlarda tutma ve düzenleyebilme.
- **Uygulama & Kasa PIN Kilidi + Windows Güvenliği Destekli Sıfırlama:**
  - Uygulama açılışına veya sadece IMEI Kasası sekmesine isteğe bağlı PIN / parola koruması koyabilme.
  - **Şifremi Unuttum Koruması:** PIN unutulduğunda 2 aşamalı 10'ar saniyelik güvenlik beklemesi ve bilgisayarın yerel **Windows Güvenliği (Windows PIN / Parola)** doğrulaması ile yetkisiz kişilerin verileri silmesini engelleyerek güvenli sıfırlama.
- **TXT İçe ve Dışa Aktarma:** Kasa kayıtlarını `.txt` formatında dışa aktarma ve dışarıdaki `.txt` listelerini (düz liste veya detaylı format) kasaya topluca içe aktarma.
- **Karanlık / Aydınlık Tema:** Göz yormayan, okunabilirliği yüksek modern karanlık ve aydınlık tema desteği.
- **Taşınabilir (Portable):** Tek parça `.exe` olarak kurulumsuz çalışabilir.

---

### Gereksinimler & Kurulum

#### 1. Kaynak Koddan Derleme
Projeyi Visual Studio veya .NET CLI ile derlemek için:
- **.NET 8.0 SDK (Windows)**
- **Python 3.10+** (requests, Pillow, onnxruntime veya opencv paketleri ile)

```bash
# Depoyu klonlayın
git clone https://github.com/istemihangaziparmaksiz/remofix_imei.git
cd remofix_imei

# Bağımlılıkları yükleyin ve derleyin
dotnet build -c Release
```

#### 2. Tek Dosya (Standalone EXE) Olarak Yayımlama
Herhangi bir DLL veya ek dosya gerektirmeyen tek bir `Remofix.exe` oluşturmak için:

```bash
dotnet publish -c Release -r source -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

### Güvenlik & Gizlilik
Uygulama sorgulanan hiçbir veriyi geliştirici sunucusuna veya üçüncü taraf API'lere göndermez. Tüm sorgular doğrudan istemci makineden resmi e-Devlet kapısına iletilir ve yerel olarak depolanır.

---

### Lisans
Kişisel ve ticari servis kullanımı için uygundur.
