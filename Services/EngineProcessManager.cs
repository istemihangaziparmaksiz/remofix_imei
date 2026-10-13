using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ImeiStudio.Services
{
    public class EngineProcessManager
    {
        private static Process? _engineProcess;
        private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(3) };

        public static async Task EnsureEngineRunningAsync()
        {
            // Önce port 5892'de çalışan var mı kontrol et
            if (await IsEngineHealthyAsync())
            {
                return;
            }

            // Gömülü kaynaktan Python scriptini oku (diske dosya çıkarmadan bellekte çalıştır)
            string? pythonScript = GetEmbeddedEngineScript();

            if (!string.IsNullOrEmpty(pythonScript))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        Arguments = "-X utf8 -",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                    _engineProcess = Process.Start(psi);
                    if (_engineProcess != null)
                    {
                        using (var writer = new StreamWriter(_engineProcess.StandardInput.BaseStream, Encoding.UTF8))
                        {
                            writer.Write(pythonScript);
                            writer.Flush();
                        }
                        _engineProcess.StandardInput.Close();

                        // Motorun ayağa kalkması için bekle
                        for (int i = 0; i < 20; i++)
                        {
                            await Task.Delay(400);
                            if (await IsEngineHealthyAsync())
                            {
                                return;
                            }
                        }
                    }
                }
                catch { }
            }

            // Fallback: Kurulu BasitUNLOCK_Server.exe varsa başlat
            string installedExe = @"C:\Program Files\BasitUNLOCK Query\engine\BasitUNLOCK_Server.exe";
            if (File.Exists(installedExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = installedExe,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    _engineProcess = Process.Start(psi);

                    for (int i = 0; i < 15; i++)
                    {
                        await Task.Delay(400);
                        if (await IsEngineHealthyAsync())
                        {
                            return;
                        }
                    }
                }
                catch { }
            }
        }

        private static string? GetEmbeddedEngineScript()
        {
            try
            {
                var assembly = typeof(EngineProcessManager).Assembly;
                string? resName = System.Linq.Enumerable.FirstOrDefault(
                    assembly.GetManifestResourceNames(),
                    n => n.EndsWith("server_engine.py", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(resName))
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        return reader.ReadToEnd();
                    }
                }
            }
            catch { }

            // Geliştirme ortamı kontrolü
            try
            {
                string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Engine\server_engine.py"));
                if (File.Exists(devPath))
                {
                    return File.ReadAllText(devPath, Encoding.UTF8);
                }
            }
            catch { }

            return null;
        }

        public static async Task<bool> IsEngineHealthyAsync()
        {
            try
            {
                var res = await _client.GetAsync("http://127.0.0.1:5892/api/health");
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static void StopEngine()
        {
            try
            {
                if (_engineProcess != null && !_engineProcess.HasExited)
                {
                    _engineProcess.Kill(true);
                    _engineProcess.Dispose();
                }
            }
            catch { }
        }
    }
}
