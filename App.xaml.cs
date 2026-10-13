using System.Windows;
using ImeiStudio.Services;

namespace ImeiStudio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EmbeddedStorageService.Initialize();
            ThemeService.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            EmbeddedStorageService.CleanupOldTempFiles();
        }
    }
}
