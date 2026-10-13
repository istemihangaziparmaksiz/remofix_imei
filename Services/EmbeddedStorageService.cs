using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ImeiStudio.Services
{
    public class EmbeddedPackage
    {
        public bool IsDarkTheme { get; set; } = true;
        public byte[]? VaultData { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Remofix Taşınabilir Gömülü Depolama Servisi
    /// Verileri (Şifreli IMEI Kasası ve Tema Tercihi) harici AppData veya yerel klasörlerde
    /// dosya oluşturmadan, doğrudan çalışan Remofix.exe dosyasının PE veri bloğu (overlay)
    /// üzerinde taşır ve günceller.
    /// </summary>
    public static class EmbeddedStorageService
    {
        private static readonly byte[] MagicFooter = Encoding.ASCII.GetBytes("<<RF_PAYLOAD_V1"); // 15 bytes
        private static readonly byte[] MagicTag = Encoding.ASCII.GetBytes("RFEND!");          // 6 bytes
        // Footer: 15 bytes (MagicFooter) + 4 bytes (payload length) + 6 bytes (MagicTag) = 25 bytes

        private static EmbeddedPackage? _currentPackage;
        private static readonly object _syncLock = new();
        private static string? _cachedExePath;

        public static string GetExecutablePath()
        {
            if (_cachedExePath != null) return _cachedExePath;

            string? path = null;
            try
            {
                path = Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch { }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = Environment.ProcessPath;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Remofix.exe");
            }

            _cachedExePath = Path.GetFullPath(path);
            return _cachedExePath;
        }

        public static void Initialize()
        {
            CleanupOldTempFiles();
            LoadPackage();
        }

        public static void CleanupOldTempFiles()
        {
            try
            {
                string exePath = GetExecutablePath();
                string dir = Path.GetDirectoryName(exePath)!;
                string tmpPath = exePath + ".tmp";

                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch { }
                }

                // Ayrıca dizindeki eski remofix*.tmp dosyalarını da temizle
                foreach (var f in Directory.GetFiles(dir, "*.tmp"))
                {
                    if (Path.GetFileName(f).StartsWith("Remofix", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        public static EmbeddedPackage LoadPackage()
        {
            lock (_syncLock)
            {
                if (_currentPackage != null)
                {
                    return _currentPackage;
                }

                string exePath = GetExecutablePath();
                _currentPackage = ReadPackageFromExe(exePath);

                if (_currentPackage == null)
                {
                    // Exe içinde henüz veri yoksa (ilk derleme), eski AppData veya Data klasöründen taşıma yap
                    _currentPackage = MigrateFromExternalStorage();
                    if (_currentPackage != null && _currentPackage.VaultData != null)
                    {
                        // İlk kez exe içine yaz
                        SavePackage(_currentPackage);
                    }
                    else
                    {
                        _currentPackage = new EmbeddedPackage { IsDarkTheme = true };
                    }
                }

                return _currentPackage;
            }
        }

        private static EmbeddedPackage? ReadPackageFromExe(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return null;

                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long footerSize = MagicFooter.Length + 4 + MagicTag.Length; // 15 + 4 + 6 = 25
                if (fs.Length <= footerSize) return null;

                fs.Seek(fs.Length - footerSize, SeekOrigin.Begin);

                byte[] footerBytes = new byte[MagicFooter.Length];
                fs.Read(footerBytes, 0, footerBytes.Length);

                byte[] lenBytes = new byte[4];
                fs.Read(lenBytes, 0, 4);
                int payloadLen = BitConverter.ToInt32(lenBytes, 0);

                byte[] tagBytes = new byte[MagicTag.Length];
                fs.Read(tagBytes, 0, tagBytes.Length);

                string ftrStr = Encoding.ASCII.GetString(footerBytes);
                string tagStr = Encoding.ASCII.GetString(tagBytes);

                if (ftrStr == "<<RF_PAYLOAD_V1" && tagStr == "RFEND!" && payloadLen > 0 && payloadLen < (fs.Length - footerSize))
                {
                    fs.Seek(fs.Length - footerSize - payloadLen, SeekOrigin.Begin);
                    byte[] payloadBytes = new byte[payloadLen];
                    fs.Read(payloadBytes, 0, payloadLen);

                    string json = Encoding.UTF8.GetString(payloadBytes);
                    return JsonSerializer.Deserialize<EmbeddedPackage>(json);
                }
            }
            catch { }

            return null;
        }

        public static void SavePackage(EmbeddedPackage package)
        {
            lock (_syncLock)
            {
                _currentPackage = package;
                package.UpdatedAt = DateTime.UtcNow;

                try
                {
                    string exePath = GetExecutablePath();
                    if (!File.Exists(exePath)) return;

                    string json = JsonSerializer.Serialize(package);
                    byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

                    long footerSize = MagicFooter.Length + 4 + MagicTag.Length;
                    long baseLength = GetBaseExeLength(exePath, footerSize);

                    string tmpPath = exePath + ".tmp";

                    // Çalışan exe kilitli mi kontrol et
                    bool canWriteDirectly = false;
                    try
                    {
                        using var test = new FileStream(exePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                        canWriteDirectly = true;
                    }
                    catch
                    {
                        canWriteDirectly = false;
                    }

                    if (!canWriteDirectly)
                    {
                        // 1. Kilitli exe dosyasını .tmp olarak yeniden adlandır (Windows buna izin verir)
                        if (File.Exists(tmpPath))
                        {
                            try { File.Delete(tmpPath); } catch { }
                        }
                        File.Move(exePath, tmpPath);

                        // 2. Orijinal exePath konumuna yeni Remofix.exe oluştur
                        using (var src = new FileStream(tmpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var dst = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                        {
                            // Sadece temel PE dosyasını kopyala (varsa eski overlay hariç)
                            byte[] buffer = new byte[81920];
                            long remaining = baseLength;
                            while (remaining > 0)
                            {
                                int toRead = (int)Math.Min(buffer.Length, remaining);
                                int read = src.Read(buffer, 0, toRead);
                                if (read <= 0) break;
                                dst.Write(buffer, 0, read);
                                remaining -= read;
                            }

                            // Yeni veriyi ve ayak izini yaz
                            dst.Write(payloadBytes, 0, payloadBytes.Length);
                            dst.Write(MagicFooter, 0, MagicFooter.Length);
                            dst.Write(BitConverter.GetBytes(payloadBytes.Length), 0, 4);
                            dst.Write(MagicTag, 0, MagicTag.Length);
                        }

                        // Çıkışta .tmp dosyasını otomatik silecek küçük arka plan komutu hazırla
                        RegisterExitCleaner(tmpPath);
                    }
                    else
                    {
                        // Doğrudan yazma imkanı varsa (oturum içi 2. ve sonraki kayıtlar)
                        using var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        fs.SetLength(baseLength); // Eski eki buda
                        fs.Seek(baseLength, SeekOrigin.Begin);
                        fs.Write(payloadBytes, 0, payloadBytes.Length);
                        fs.Write(MagicFooter, 0, MagicFooter.Length);
                        fs.Write(BitConverter.GetBytes(payloadBytes.Length), 0, 4);
                        fs.Write(MagicTag, 0, MagicTag.Length);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EmbeddedStorageService Save Error: {ex.Message}");
                }
            }
        }

        private static long GetBaseExeLength(string exePath, long footerSize)
        {
            try
            {
                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length <= footerSize) return fs.Length;

                fs.Seek(fs.Length - footerSize, SeekOrigin.Begin);
                byte[] footerBytes = new byte[MagicFooter.Length];
                fs.Read(footerBytes, 0, footerBytes.Length);

                byte[] lenBytes = new byte[4];
                fs.Read(lenBytes, 0, 4);
                int payloadLen = BitConverter.ToInt32(lenBytes, 0);

                byte[] tagBytes = new byte[MagicTag.Length];
                fs.Read(tagBytes, 0, tagBytes.Length);

                if (Encoding.ASCII.GetString(footerBytes) == "<<RF_PAYLOAD_V1" &&
                    Encoding.ASCII.GetString(tagBytes) == "RFEND!" &&
                    payloadLen > 0 &&
                    payloadLen < (fs.Length - footerSize))
                {
                    return fs.Length - (payloadLen + footerSize);
                }

                return fs.Length;
            }
            catch
            {
                return new FileInfo(exePath).Length;
            }
        }

        private static void RegisterExitCleaner(string tmpPath)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c ping 127.0.0.1 -n 2 > nul & del /f /q \"{tmpPath}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch { }
            };
        }

        private static EmbeddedPackage? MigrateFromExternalStorage()
        {
            try
            {
                byte[]? vaultData = null;
                bool isDark = true;

                // 1. LocalAppData kontrol et
                string appDataVault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Remofix", "remofix_vault.dat");
                string appDataTheme = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Remofix", "theme.cfg");

                if (File.Exists(appDataVault))
                {
                    vaultData = File.ReadAllBytes(appDataVault);
                }
                if (File.Exists(appDataTheme) && bool.TryParse(File.ReadAllText(appDataTheme).Trim(), out bool d))
                {
                    isDark = d;
                }

                // 2. BaseDirectory / Data klasörü kontrol et
                if (vaultData == null)
                {
                    string oldVault = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "remofix_vault.dat");
                    if (File.Exists(oldVault))
                    {
                        vaultData = File.ReadAllBytes(oldVault);
                    }
                }

                if (vaultData != null)
                {
                    // Taşındıktan sonra AppData/Remofix klasörünü temizle
                    try
                    {
                        string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Remofix");
                        if (Directory.Exists(appDataDir))
                        {
                            Directory.Delete(appDataDir, true);
                        }
                    }
                    catch { }

                    return new EmbeddedPackage
                    {
                        IsDarkTheme = isDark,
                        VaultData = vaultData
                    };
                }
            }
            catch { }

            return null;
        }
    }
}
