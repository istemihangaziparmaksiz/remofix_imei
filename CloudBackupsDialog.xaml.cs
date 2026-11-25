using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ImeiStudio.Services;
using ImeiStudio.ViewModels;

namespace ImeiStudio
{
    public partial class CloudBackupsDialog : Window
    {
        private readonly MainViewModel _vm;
        public bool WasRestored { get; private set; } = false;

        public CloudBackupsDialog(MainViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            InitializeComponent();
            Loaded += async (s, e) => await LoadCloudBackupsAsync();
        }

        private async Task LoadCloudBackupsAsync()
        {
            txtCloudStatus.Text = "Google Drive yedekleri taranıyor...";
            btnDownloadAndRestore.IsEnabled = false;

            var items = await GoogleDriveSyncService.ListCloudBackupsAsync();
            dgCloudBackups.ItemsSource = items;

            txtCloudStatus.Text = items.Count > 0
                ? $"Google Drive'da {items.Count} adet bulut yedeği bulundu."
                : "Google Drive'da henüz bir yedek dosyası (.rfbak) bulunamadı.";
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadCloudBackupsAsync();
        }

        private void dgCloudBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCloudBackups.SelectedItem is DriveCloudBackupItem item)
            {
                btnDownloadAndRestore.IsEnabled = true;
                txtSelectedCloudInfo.Text = $"Seçildi: {item.Name} ({item.DisplaySize})";
            }
            else
            {
                btnDownloadAndRestore.IsEnabled = false;
                txtSelectedCloudInfo.Text = "İndirip geri yüklemek istediğiniz bulut yedeğini seçin.";
            }
        }

        private async void BtnDownloadAndRestore_Click(object sender, RoutedEventArgs e)
        {
            if (dgCloudBackups.SelectedItem is not DriveCloudBackupItem selected) return;

            string targetDir = VaultBackupService.BackupDirectory;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, selected.Name);

            btnDownloadAndRestore.IsEnabled = false;
            txtSelectedCloudInfo.Text = "Yedek dosyası Google Drive'dan indiriliyor...";

            bool downloaded = await GoogleDriveSyncService.DownloadCloudBackupAsync(selected.Id, targetPath);
            if (!downloaded || !File.Exists(targetPath))
            {
                MessageBox.Show(
                    "Yedek dosyası Google Drive'dan indirilemedi. Lütfen internet bağlantınızı kontrol edin.",
                    "İndirme Hatası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                txtSelectedCloudInfo.Text = "İndirme başarısız oldu.";
                btnDownloadAndRestore.IsEnabled = true;
                return;
            }

            var info = VaultBackupService.InspectBackupFile(targetPath);
            if (info == null || !info.IsValid)
            {
                MessageBox.Show(
                    "İndirilen dosya geçerli bir Remofix kasa yedeği değil veya bozulmuş.",
                    "Geçersiz Dosya",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            string? requiredPin = null;
            if (info.IsPinProtected && !info.IsUnlocked)
            {
                var pinDlg = new PinPromptDialog(
                    $"'{info.FileName}' bulut yedeğini açmak için lütfen bu yedeğin güvenlik PIN kodunu girin.",
                    forExternalPinPrompt: true)
                {
                    Owner = this
                };

                if (pinDlg.ShowDialog() != true)
                {
                    btnDownloadAndRestore.IsEnabled = true;
                    return;
                }

                requiredPin = pinDlg.EnteredPin;
                var verifiedRecords = VaultBackupService.RestoreFromBackup(targetPath, requiredPin);
                if (verifiedRecords == null)
                {
                    MessageBox.Show(
                        "Hatalı PIN kodu girdiniz!\n\nBu bulut yedeği sahibinin özel PIN kodu ile mühürlenmiştir. Doğru PIN girilmeden yedeğin içeriği açılamaz veya geri yüklenemez.",
                        "PIN Doğrulama Başarısız",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    btnDownloadAndRestore.IsEnabled = true;
                    return;
                }

                info.TotalRecords = verifiedRecords.Count;
                info.UsableRecords = verifiedRecords.Count(r => !r.IsUsed);
                info.UsedRecords = verifiedRecords.Count(r => r.IsUsed);
                info.IsUnlocked = true;
            }

            var confirm = MessageBox.Show(
                $"İndirilen bulut yedeğini kasaya geri yüklemek istiyor musunuz?\n\n" +
                $"• Dosya: {info.FileName}\n" +
                $"• Toplam IMEI: {info.TotalRecords} adet ({info.UsableRecords} Kullanılabilir, {info.UsedRecords} Kullanılan)\n" +
                (info.IsPinProtected ? "• Koruma Düzeyi: 🔒 Kullanıcı PIN Kodu ile Mühürlü\n\n" : "\n") +
                $"Not: Mevcut kasanız güvenlik amacıyla işlem öncesinde otomatik olarak yedeklenecektir.",
                "Bulut Yedeğini Geri Yükle",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                bool success = _vm.RestoreVaultFromBackupFile(targetPath, requiredPin);
                if (success)
                {
                    WasRestored = true;
                    MessageBox.Show(
                        $"{info.TotalRecords} adet IMEI kaydı bulut yedeğinden kasaya başarıyla geri yüklendi.",
                        "Geri Yükleme Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                }
            }
            else
            {
                btnDownloadAndRestore.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
