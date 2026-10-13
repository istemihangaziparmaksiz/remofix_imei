using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImeiStudio.Models;

namespace ImeiStudio.Services
{
    /// <summary>
    /// RemoFix Yerel Güvenli Kasa Depolama Servisi
    /// Dışarıdan herhangi bir veritabanı (SQL Server, MySQL, SQLite vb.) gerektirmeden,
    /// tamamen yerel olarak AES-256-CBC ve HMAC-SHA256 bütünlük doğrulaması ile
    /// şifreli kayıt tutar. Veriler doğrudan Remofix.exe içinde taşınır.
    /// </summary>
    public class VaultStorageService
    {
        private static readonly byte[] MagicHeader = Encoding.ASCII.GetBytes("RFVK"); // RemoFix Vault Key
        private const byte FormatVersion = 1;

        // Dahili türetilmiş 256-bit AES şifreleme ve HMAC anahtarları
        private static readonly byte[] EncryptionKey = new byte[32]
        {
            0x4B, 0x72, 0x65, 0x6D, 0x6F, 0x46, 0x69, 0x78,
            0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x56, 0x61,
            0x75, 0x6C, 0x74, 0x4B, 0x65, 0x79, 0x32, 0x30,
            0x32, 0x36, 0x24, 0x23, 0x21, 0x5F, 0x9A, 0xBC
        };

        private static readonly byte[] HmacKey = new byte[32]
        {
            0x9F, 0x8E, 0x7D, 0x6C, 0x5B, 0x4A, 0x39, 0x28,
            0x17, 0x06, 0xF5, 0xE4, 0xD3, 0xC2, 0xB1, 0xA0,
            0x52, 0x65, 0x6D, 0x6F, 0x46, 0x69, 0x78, 0x48,
            0x6D, 0x61, 0x63, 0x41, 0x75, 0x74, 0x68, 0x31
        };

        public VaultStorageService()
        {
        }

        public string VaultFilePath => EmbeddedStorageService.GetExecutablePath();

        /// <summary>
        /// Kasa kayıtlarını Remofix.exe içerisindeki gömülü şifreli paketten yükler ve çözer.
        /// Kayıt yoksa veya harici olarak bozulmuş/müdahale edilmişse boş liste döner.
        /// </summary>
        public List<ImeiRecord> LoadVault()
        {
            try
            {
                var pkg = EmbeddedStorageService.LoadPackage();
                byte[]? rawFileBytes = pkg.VaultData;

                if (rawFileBytes == null || rawFileBytes.Length < 69)
                {
                    return new List<ImeiRecord>();
                }

                // 1. Sihirli Başlık Kontrolü (RFVK)
                for (int i = 0; i < MagicHeader.Length; i++)
                {
                    if (rawFileBytes[i] != MagicHeader[i])
                    {
                        return new List<ImeiRecord>();
                    }
                }

                byte version = rawFileBytes[4];
                if (version != FormatVersion)
                {
                    return new List<ImeiRecord>();
                }

                // 2. IV ve HMAC Ayıklama
                byte[] iv = new byte[16];
                Buffer.BlockCopy(rawFileBytes, 5, iv, 0, 16);

                byte[] fileHmac = new byte[32];
                Buffer.BlockCopy(rawFileBytes, 21, fileHmac, 0, 32);

                int cipherLength = rawFileBytes.Length - 53;
                byte[] cipherBytes = new byte[cipherLength];
                Buffer.BlockCopy(rawFileBytes, 53, cipherBytes, 0, cipherLength);

                // 3. HMAC Bütünlük Doğrulaması
                using (var hmac = new HMACSHA256(HmacKey))
                {
                    byte[] toHash = new byte[iv.Length + cipherBytes.Length];
                    Buffer.BlockCopy(iv, 0, toHash, 0, iv.Length);
                    Buffer.BlockCopy(cipherBytes, 0, toHash, iv.Length, cipherBytes.Length);
                    byte[] computedHmac = hmac.ComputeHash(toHash);

                    if (!CryptographicOperations.FixedTimeEquals(fileHmac, computedHmac))
                    {
                        return new List<ImeiRecord>();
                    }
                }

                // 4. AES-256 Deşifreleme
                byte[] plainBytes;
                using (var aes = Aes.Create())
                {
                    aes.Key = EncryptionKey;
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
                var records = JsonSerializer.Deserialize<List<ImeiRecord>>(json);
                return records ?? new List<ImeiRecord>();
            }
            catch
            {
                return new List<ImeiRecord>();
            }
        }

        /// <summary>
        /// Kasa kayıtlarını AES-256 ile şifreleyip HMAC bütünlük kodu ekleyerek Remofix.exe içerisine gömer.
        /// </summary>
        public bool SaveVault(IEnumerable<ImeiRecord> records)
        {
            try
            {
                var list = new List<ImeiRecord>(records);
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = false });
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);

                byte[] iv = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(iv);
                }

                byte[] cipherBytes;
                using (var aes = Aes.Create())
                {
                    aes.Key = EncryptionKey;
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

                // HMAC Hesabı
                byte[] computedHmac;
                using (var hmac = new HMACSHA256(HmacKey))
                {
                    byte[] toHash = new byte[iv.Length + cipherBytes.Length];
                    Buffer.BlockCopy(iv, 0, toHash, 0, iv.Length);
                    Buffer.BlockCopy(cipherBytes, 0, toHash, iv.Length, cipherBytes.Length);
                    computedHmac = hmac.ComputeHash(toHash);
                }

                // Tam Kasa Paketi
                using var outStream = new MemoryStream();
                outStream.Write(MagicHeader, 0, MagicHeader.Length);
                outStream.WriteByte(FormatVersion);
                outStream.Write(iv, 0, iv.Length);
                outStream.Write(computedHmac, 0, computedHmac.Length);
                outStream.Write(cipherBytes, 0, cipherBytes.Length);

                byte[] finalVaultBytes = outStream.ToArray();

                // Remofix.exe içine gömülü olarak kaydet
                var pkg = EmbeddedStorageService.LoadPackage();
                pkg.VaultData = finalVaultBytes;
                EmbeddedStorageService.SavePackage(pkg);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
