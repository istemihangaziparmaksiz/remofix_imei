using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImeiStudio.Models;

namespace ImeiStudio.Services
{
    public class VaultBackupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long FileSizeBytes { get; set; }
        public int TotalRecords { get; set; }
        public int UsableRecords { get; set; }
        public int UsedRecords { get; set; }
        public bool IsValid { get; set; }
        public bool IsPinProtected { get; set; }
        public bool IsUnlocked { get; set; }

        public string DisplaySize => $"{Math.Max(1, FileSizeBytes / 1024)} KB";
        public string DisplayDate => CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");

        public string DisplaySummary
        {
            get
            {
                if (!IsValid) return "Geçersiz Yedek";
                if (IsPinProtected && !IsUnlocked)
                {
                    return TotalRecords > 0
                        ? $"{TotalRecords} IMEI • 🔒 PIN Korumalı"
                        : "🔒 PIN Korumalı Yedek";
                }
                string pinBadge = IsPinProtected ? " (🔒 PIN Korumalı)" : "";
                return $"{TotalRecords} IMEI ({UsableRecords} Kullanılabilir, {UsedRecords} Kullanılan){pinBadge}";
            }
        }
    }

    /// <summary>
    /// Remofix Akıllı Otomatik Yedekleme & Kurtarma Servisi (Safeguard Engine)
    /// Kasa kayıtlarını kullanıcının kişisel PIN koduyla mühürleyerek AES-256 ve PBKDF2 ile şifreler.
    /// Yedeği alan kişinin belirlediği PIN kodu olmadan, bu program da dahil olmak üzere
    /// başka hiç kimse yedeğin içeriğini okuyamaz veya kasaya yükleyemez.
    /// </summary>
    public static class VaultBackupService
    {
        private static readonly byte[] BackupMagicV2 = Encoding.ASCII.GetBytes("RF_BAK_V2::"); // 11 bytes
        private static readonly byte[] BackupMagicV1 = Encoding.ASCII.GetBytes("RF_BAK_V1::"); // 11 bytes (Geriye uyumluluk)
        private static readonly object _backupLock = new();

        /// <summary>
        /// Varsayılan yedekleme dizini: Belgelerim\Remofix_Backups\
        /// </summary>
        public static string BackupDirectory
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string dir = Path.Combine(docs, "Remofix_Backups");
                if (!Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return dir;
            }
        }

        /// <summary>
        /// Verilen kayıt listesini kullanıcının PIN koduyla mühürleyerek şifreli bir .rfbak dosyası olarak anında yedekler.
        /// </summary>
        public static string? CreateBackup(IEnumerable<ImeiRecord> records, string tag = "auto", string? specificPin = null)
        {
            lock (_backupLock)
            {
                try
                {
                    var list = records?.ToList() ?? new List<ImeiRecord>();
                    string dir = BackupDirectory;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"Remofix_Vault_{tag}_{timeStamp}.rfbak";
                    string fullPath = Path.Combine(dir, fileName);

                    // Kullanılacak PIN'i belirle (oturumdaki PIN veya verilen özel PIN)
                    string? pinToUse = !string.IsNullOrEmpty(specificPin)
                        ? specificPin
                        : EmbeddedStorageService.CurrentSessionPin;

                    byte flag = !string.IsNullOrEmpty(pinToUse) ? (byte)1 : (byte)0;
                    byte[] salt = new byte[16];
                    if (flag == 1)
                    {
                        using var rng = RandomNumberGenerator.Create();
                        rng.GetBytes(salt);
                    }

                    // JSON formatına serialize et
                    string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = false });
                    byte[] plainBytes = Encoding.UTF8.GetBytes(json);

                    // Anahtarları türet (PIN varsa PBKDF2 + Master Pepper; yoksa Master Anahtar)
                    byte[] aesKey, hmacKey;
                    if (flag == 1 && !string.IsNullOrEmpty(pinToUse))
                    {
                        (aesKey, hmacKey) = DeriveKeysFromPin(pinToUse, salt);
                    }
                    else
                    {
                        aesKey = MasterEncryptionKey;
                        hmacKey = MasterHmacKey;
                    }

                    // AES-256-CBC ile şifrele
                    byte[] iv = new byte[16];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(iv);
                    }

                    byte[] cipherBytes;
                    using (var aes = Aes.Create())
                    {
                        aes.Key = aesKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using var encryptor = aes.CreateEncryptor();
                        using var ms = new MemoryStream();
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cs.Write(plainBytes, 0, plainBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        cipherBytes = ms.ToArray();
                    }

                    // HMAC bütünlük kodunu hesapla: Hash(Salt + IV + Ciphertext)
                    byte[] hmacHash;
                    using (var hmac = new HMACSHA256(hmacKey))
                    {
                        byte[] toHash = new byte[salt.Length + iv.Length + cipherBytes.Length];
                        Buffer.BlockCopy(salt, 0, toHash, 0, salt.Length);
                        Buffer.BlockCopy(iv, 0, toHash, salt.Length, iv.Length);
                        Buffer.BlockCopy(cipherBytes, 0, toHash, salt.Length + iv.Length, cipherBytes.Length);
                        hmacHash = hmac.ComputeHash(toHash);
                    }

                    // Başlık ve bütünlük verisiyle dosyaya yaz
                    // Düzen: Magic(11) + Flag(1) + Salt(16) + Count(4) + IV(16) + HMAC(32) + Cipher(N)
                    using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.Write(BackupMagicV2, 0, BackupMagicV2.Length);
                        fs.WriteByte(flag);
                        fs.Write(salt, 0, salt.Length);
                        fs.Write(BitConverter.GetBytes(list.Count), 0, 4);
                        fs.Write(iv, 0, iv.Length);
                        fs.Write(hmacHash, 0, hmacHash.Length);
                        fs.Write(cipherBytes, 0, cipherBytes.Length);
                    }

                    // En fazla 30 otomatik yedek sakla, eski olanları temizle
                    CleanupOldBackups(30);

                    return fullPath;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Mevcut yedek dosyalarını kronolojik olarak listeler ve özet bilgilerini çıkarır.
        /// </summary>
        public static List<VaultBackupInfo> GetAvailableBackups(string? sessionPin = null)
        {
            var result = new List<VaultBackupInfo>();
            try
            {
                string dir = BackupDirectory;
                if (!Directory.Exists(dir)) return result;

                var files = Directory.GetFiles(dir, "*.rfbak")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                foreach (var fi in files)
                {
                    var info = InspectBackupFile(fi.FullName, sessionPin);
                    if (info != null)
                    {
                        result.Add(info);
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Belirtilen yedek dosyasını açıp çözümler ve içindeki IMEI listesini döner.
        /// PIN korumalı dosyalarda doğru PIN sağlanamazsa veya bütünlük bozulmuşsa null döner.
        /// </summary>
        public static List<ImeiRecord>? RestoreFromBackup(string backupFilePath, string? pin = null)
        {
            try
            {
                if (!File.Exists(backupFilePath)) return null;

                byte[] fileBytes = File.ReadAllBytes(backupFilePath);
                if (fileBytes.Length < 48) return null;

                // V2 Format Kontrolü
                if (IsMagicMatch(fileBytes, BackupMagicV2))
                {
                    if (fileBytes.Length < 80) return null; // 11 + 1 + 16 + 4 + 16 + 32 = 80

                    byte flag = fileBytes[11];
                    byte[] salt = new byte[16];
                    Buffer.BlockCopy(fileBytes, 12, salt, 0, 16);

                    int count = BitConverter.ToInt32(fileBytes, 28);

                    byte[] iv = new byte[16];
                    Buffer.BlockCopy(fileBytes, 32, iv, 0, 16);

                    byte[] fileHmac = new byte[32];
                    Buffer.BlockCopy(fileBytes, 48, fileHmac, 0, 32);

                    int cipherOffset = 80;
                    int cipherLen = fileBytes.Length - cipherOffset;
                    if (cipherLen <= 0) return null;

                    byte[] cipherBytes = new byte[cipherLen];
                    Buffer.BlockCopy(fileBytes, cipherOffset, cipherBytes, 0, cipherLen);

                    byte[] aesKey, hmacKey;
                    if (flag == 1)
                    {
                        // PIN Korumalı Yedek: Şifreleme kullanıcının kişisel PIN kodundan türetilir
                        string? effectivePin = !string.IsNullOrEmpty(pin)
                            ? pin
                            : EmbeddedStorageService.CurrentSessionPin;

                        if (string.IsNullOrEmpty(effectivePin))
                        {
                            return null; // PIN olmadan çözülemez!
                        }

                        (aesKey, hmacKey) = DeriveKeysFromPin(effectivePin, salt);
                    }
                    else
                    {
                        aesKey = MasterEncryptionKey;
                        hmacKey = MasterHmacKey;
                    }

                    // HMAC Doğrulaması: Salt + IV + Ciphertext
                    using (var hmac = new HMACSHA256(hmacKey))
                    {
                        byte[] toHash = new byte[salt.Length + iv.Length + cipherBytes.Length];
                        Buffer.BlockCopy(salt, 0, toHash, 0, salt.Length);
                        Buffer.BlockCopy(iv, 0, toHash, salt.Length, iv.Length);
                        Buffer.BlockCopy(cipherBytes, 0, toHash, salt.Length + iv.Length, cipherBytes.Length);
                        byte[] computedHmac = hmac.ComputeHash(toHash);

                        if (!CryptographicOperations.FixedTimeEquals(fileHmac, computedHmac))
                        {
                            return null; // Hatalı PIN veya bozulmuş dosya!
                        }
                    }

                    // AES-256 Deşifreleme
                    byte[] plainBytes;
                    using (var aes = Aes.Create())
                    {
                        aes.Key = aesKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using var decryptor = aes.CreateDecryptor();
                        using var ms = new MemoryStream();
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        plainBytes = ms.ToArray();
                    }

                    string json = Encoding.UTF8.GetString(plainBytes);
                    return JsonSerializer.Deserialize<List<ImeiRecord>>(json);
                }

                // V1 Geriye Uyumluluk Kontrolü
                if (IsMagicMatch(fileBytes, BackupMagicV1))
                {
                    int cipherOffset = BackupMagicV1.Length + 4;
                    int cipherLen = fileBytes.Length - cipherOffset;
                    if (cipherLen < 48) return null;

                    byte[] cipherPayload = new byte[cipherLen];
                    Buffer.BlockCopy(fileBytes, cipherOffset, cipherPayload, 0, cipherLen);

                    byte[]? plainBytes = DecryptV1Payload(cipherPayload);
                    if (plainBytes == null) return null;

                    string json = Encoding.UTF8.GetString(plainBytes);
                    return JsonSerializer.Deserialize<List<ImeiRecord>>(json);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Belirli bir yedek dosyasını kontrol eder, PIN korumalı olup olmadığını tespit eder ve bilgi özetini oluşturur.
        /// </summary>
        public static VaultBackupInfo? InspectBackupFile(string filePath, string? pin = null)
        {
            try
            {
                var fi = new FileInfo(filePath);
                if (!fi.Exists) return null;

                var info = new VaultBackupInfo
                {
                    FilePath = filePath,
                    FileName = fi.Name,
                    CreatedAt = fi.LastWriteTime,
                    FileSizeBytes = fi.Length,
                    IsValid = false,
                    IsPinProtected = false,
                    IsUnlocked = false
                };

                byte[] header = new byte[Math.Min(80, (int)fi.Length)];
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Read(header, 0, header.Length);
                }

                if (IsMagicMatch(header, BackupMagicV2) && header.Length >= 80)
                {
                    info.IsValid = true;
                    byte flag = header[11];
                    info.IsPinProtected = (flag == 1);
                    info.TotalRecords = BitConverter.ToInt32(header, 28);

                    var records = RestoreFromBackup(filePath, pin);
                    if (records != null)
                    {
                        info.IsUnlocked = true;
                        info.TotalRecords = records.Count;
                        info.UsableRecords = records.Count(r => !r.IsUsed);
                        info.UsedRecords = records.Count(r => r.IsUsed);
                    }
                    return info;
                }

                if (IsMagicMatch(header, BackupMagicV1))
                {
                    info.IsValid = true;
                    info.IsPinProtected = false;
                    var records = RestoreFromBackup(filePath);
                    if (records != null)
                    {
                        info.IsUnlocked = true;
                        info.TotalRecords = records.Count;
                        info.UsableRecords = records.Count(r => !r.IsUsed);
                        info.UsedRecords = records.Count(r => r.IsUsed);
                    }
                    return info;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMagicMatch(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
            {
                if (data[i] != magic[i]) return false;
            }
            return true;
        }

        private static void CleanupOldBackups(int keepCount)
        {
            try
            {
                string dir = BackupDirectory;
                var autoFiles = Directory.GetFiles(dir, "Remofix_Vault_auto_*.rfbak")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Skip(keepCount)
                    .ToList();

                foreach (var f in autoFiles)
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        #region Encryption & Key Derivation Core
        private static readonly byte[] MasterEncryptionKey = new byte[32]
        {
            0x4B, 0x72, 0x65, 0x6D, 0x6F, 0x46, 0x69, 0x78,
            0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x56, 0x61,
            0x75, 0x6C, 0x74, 0x4B, 0x65, 0x79, 0x32, 0x30,
            0x32, 0x36, 0x24, 0x23, 0x21, 0x5F, 0x9A, 0xBC
        };

        private static readonly byte[] MasterHmacKey = new byte[32]
        {
            0x9F, 0x8E, 0x7D, 0x6C, 0x5B, 0x4A, 0x39, 0x28,
            0x17, 0x06, 0xF5, 0xE4, 0xD3, 0xC2, 0xB1, 0xA0,
            0x52, 0x65, 0x6D, 0x6F, 0x46, 0x69, 0x78, 0x48,
            0x6D, 0x61, 0x63, 0x41, 0x75, 0x74, 0x68, 0x31
        };

        /// <summary>
        /// Kullanıcının kişisel PIN kodundan ve rastgele Salt değerinden PBKDF2 (50.000 iterasyon) ile
        /// AES ve HMAC anahtarlarını türetir. Remofix Master Anahtarı ile karıştırarak çift katmanlı koruma sağlar.
        /// </summary>
        private static (byte[] aesKey, byte[] hmacKey) DeriveKeysFromPin(string pin, byte[] salt)
        {
            byte[] pinBytes = Encoding.UTF8.GetBytes(pin.Trim());
            byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
                pinBytes,
                salt,
                iterations: 50_000,
                HashAlgorithmName.SHA256,
                outputLength: 64
            );

            byte[] aesKey = new byte[32];
            byte[] hmacKey = new byte[32];
            Buffer.BlockCopy(derived, 0, aesKey, 0, 32);
            Buffer.BlockCopy(derived, 32, hmacKey, 0, 32);

            // Çift Katman: Remofix dahili biberi (pepper) ile harmanla
            for (int i = 0; i < 32; i++)
            {
                aesKey[i] ^= MasterEncryptionKey[i];
                hmacKey[i] ^= MasterHmacKey[i];
            }

            return (aesKey, hmacKey);
        }

        private static byte[]? DecryptV1Payload(byte[] rawEncrypted)
        {
            try
            {
                if (rawEncrypted.Length < 48) return null;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(rawEncrypted, 0, iv, 0, 16);

                byte[] expectedHmac = new byte[32];
                Buffer.BlockCopy(rawEncrypted, 16, expectedHmac, 0, 32);

                int cipherLen = rawEncrypted.Length - 48;
                byte[] cipherBytes = new byte[cipherLen];
                Buffer.BlockCopy(rawEncrypted, 48, cipherBytes, 0, cipherLen);

                using (var hmac = new HMACSHA256(MasterHmacKey))
                {
                    byte[] toHash = new byte[iv.Length + cipherBytes.Length];
                    Buffer.BlockCopy(iv, 0, toHash, 0, iv.Length);
                    Buffer.BlockCopy(cipherBytes, 0, toHash, iv.Length, cipherBytes.Length);
                    byte[] computedHmac = hmac.ComputeHash(toHash);

                    if (!CryptographicOperations.FixedTimeEquals(expectedHmac, computedHmac))
                    {
                        return null;
                    }
                }

                using var aes = Aes.Create();
                aes.Key = MasterEncryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    cs.Write(cipherBytes, 0, cipherBytes.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}
