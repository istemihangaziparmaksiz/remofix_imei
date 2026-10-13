# Remofix IMEI

BTK / e-Devlet servisleri üzerinden tekli ve toplu IMEI sorgulaması yapmak, dönen sonuçları yerel kasa içinde arşivlemek ve takip etmek için geliştirilmiş bağımsız bir Windows masaüstü aracıdır.

Saha teknisyenleri ve telefon servisleri için tekrarlayan sorgulama süreçlerini hızlandırmak ve IMEI kayıtlarını harici bir sunucuya veya veritabanına ihtiyaç duymadan güvenle saklamak amacıyla tasarlanmıştır.

---

### Özellikler

- **Tekli & Toplu Sorgulama:** 
  - 14 hane girildiğinde Luhn algoritmasıyla 15. kontrol basamağını (Check Digit) anında otomatik hesaplar.
  - Yüzlerce IMEI numarasını tek seferde liste halinde kuyruğa alıp sorgulayabilir.
- **Dahili Doğrulama Motoru:** Arka planda çalışan optimize edilmiş captcha motoru sayesinde doğrulama kodlarını otomatik çözer, e-Devlet kapısından marka/model ve kayıt durumunu çeker.
- **Yerel ve Şifreli IMEI Kasası:**
  - Herhangi bir bulut servisine, SQL veya uzak sunucuya bağlanmaz; tamamen yereldir.
  - Veriler AES-256-CBC ve HMAC-SHA256 ile şifrelenir, dışarıdan kurcalamaya karşı korumalıdır.
  - Tekli sorgulamalarda sonuçlar kullanıcının isteğine göre tek tıkla kasaya aktarılır.
- **Kullanım & Durum Yönetimi:**
  - Cihazları "Kullanımda", "Boşta" veya özel notlarla etiketleme imkanı.
  - Hızlı arama, kategori filtreleme (Kayıtlı, İthalat Yoluyla Kaydedilen, Kayıtsız vb.).
- **Dışa Aktarma:** Seçili veya tüm sonuçları Excel, CSV veya panoya doğrudan kopyalama.
- **Karanlık / Aydınlık Tema:** ModernWPF tabanlı, sistemle uyumlu arayüz.
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
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o ./publish
```

---

### Güvenlik & Gizlilik
Uygulama sorgulanan hiçbir veriyi geliştirici sunucusuna veya üçüncü taraf API'lere göndermez. Tüm sorgular doğrudan istemci makineden resmi e-Devlet kapısına iletilir ve yerel olarak depolanır.

---

### Lisans
Kişisel ve ticari servis kullanımı için uygundur.
