using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ImeiStudio.Services;
using ImeiStudio.ViewModels;
using Microsoft.Win32;

namespace ImeiStudio
{
    public partial class VaultBackupDialog : Window
    {
        private readonly MainViewModel _vm;
        public bool WasVaultRestored { get; private set; } = false;
        private bool _isDriveConnected = false;

        public VaultBackupDialog(MainViewModel vm)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            InitializeComponent();
            RefreshBackupList();
            Loaded += async (s, e) => await InitDriveStateAsync();
        }

        private void RefreshBackupList()
        {
            var backups = VaultBackupService.GetAvailableBackups();
            dgBackups.ItemsSource = backups;

            txtBackupStats.Text = backups.Count > 0
                ? $"Toplam {backups.Count} adet geçerli kasa yedeği mevcut."
                : "Henüz oluşturulmuş bir kasa yedeği bulunmuyor.";

            txtBackupPath.Text = $"Konum: {VaultBackupService.BackupDirectory}";
            btnRestoreSelected.IsEnabled = false;
            btnRestoreSelected.Content = "🔄 Bu Yedeği Geri Yükle";
            txtSelectedSummary.Text = "Geri yüklemek istediğiniz yedeği listeden seçin.";
        }

        #region Google Drive Integration
        private async Task InitDriveStateAsync()
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            chkDriveAutoUpload.IsChecked = pkg.GoogleDriveAutoUpload;

            await UpdateDriveStatusUiAsync();
        }

        private async Task UpdateDriveStatusUiAsync()
        {
            string? email = await GoogleDriveSyncService.GetConnectedAccountAsync();
            _isDriveConnected = !string.IsNullOrEmpty(email);

            if (_isDriveConnected)
            {
                txtDriveBadge.Text = "BAĞLI";
                borderDriveBadge.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                txtDriveBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

                txtDriveStatus.Text = $"Hesap: {email} • Drive/Remofix_Backups";
                btnDriveConnect.Content = "Bağlantıyı Kes";
                btnDriveBrowseCloud.IsEnabled = true;
            }
            else
            {
                txtDriveBadge.Text = "BAĞLI DEĞİL";
                borderDriveBadge.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                txtDriveBadge.Foreground = (Brush)Application.Current.Resources["TextMutedBrush"];

                txtDriveStatus.Text = "Yedekler yalnızca bu bilgisayarda saklanıyor.";
                btnDriveConnect.Content = "☁️ Google ile Bağlan";
                btnDriveBrowseCloud.IsEnabled = false;
            }
        }

        private async void BtnDriveConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isDriveConnected)
            {
                var confirm = MessageBox.Show(
                    "Google Drive bulut bağlantısını kesmek istediğinizden emin misiniz?\n\n" +
                    "Bağlantı kesildiğinde yeni kasa yedekleri otomatik olarak Drive'a yüklenmeyecektir.",
                    "Google Drive Bağlantısını Kes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    await GoogleDriveSyncService.SignOutAsync();
                    await UpdateDriveStatusUiAsync();
                }
                return;
            }

            btnDriveConnect.IsEnabled = false;
            btnDriveConnect.Content = "Giriş Bekleniyor...";

            try
            {
                var (success, email, error) = await GoogleDriveSyncService.SignInAsync();
                if (success)
                {
                    await UpdateDriveStatusUiAsync();
                    MessageBox.Show(
                        $"Google Drive bağlantısı başarıyla kuruldu!\n\nBağlı Hesap: {email}\n\nKasa yedekleriniz Google Drive hesabınızdaki 'Remofix_Backups' klasörüne otomatik olarak eşitlenecektir.",
                        "Google Drive Bağlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Mevcut en son yerel yedeği arka planda hemen Drive'a yükle
                    _ = Task.Run(async () =>
                    {
                        var localBackups = VaultBackupService.GetAvailableBackups();
                        if (localBackups.Count > 0)
                        {
                            await GoogleDriveSyncService.UploadBackupFileAsync(localBackups[0].FilePath);
                        }
                    });
                }
                else
                {
                    MessageBox.Show(
                        $"Google ile oturum açılamadı: {error}",
                        "Bağlantı Hatası",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                btnDriveConnect.IsEnabled = true;
                if (!_isDriveConnected)
                {
                    btnDriveConnect.Content = "☁️ Google ile Bağlan";
                }
            }
        }

        private void BtnDriveBrowseCloud_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CloudBackupsDialog(_vm) { Owner = this };
            dlg.ShowDialog();
            if (dlg.WasRestored)
            {
                WasVaultRestored = true;
                RefreshBackupList();
            }
        }

        private void ChkDriveAutoUpload_Changed(object sender, RoutedEventArgs e)
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            pkg.GoogleDriveAutoUpload = chkDriveAutoUpload.IsChecked == true;
            EmbeddedStorageService.SavePackage(pkg);
        }
        #endregion

        private void dgBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgBackups.SelectedItem is VaultBackupInfo selected && selected.IsValid)
            {
                btnRestoreSelected.IsEnabled = true;
                btnRestoreSelected.Content = selected.IsPinProtected && !selected.IsUnlocked
                    ? "🔒 PIN ile Geri Yükle"
                    : "🔄 Bu Yedeği Geri Yükle";
                txtSelectedSummary.Text = $"Seçildi: {selected.FileName} ({selected.DisplaySummary})";
            }
            else
            {
                btnRestoreSelected.IsEnabled = false;
                btnRestoreSelected.Content = "🔄 Bu Yedeği Geri Yükle";
                txtSelectedSummary.Text = "Geri yüklemek istediğiniz yedeği listeden seçin.";
            }
        }

        private void BtnCreateManualBackup_Click(object sender, RoutedEventArgs e)
        {
            if (EmbeddedStorageService.HasPinConfigured() && string.IsNullOrEmpty(EmbeddedStorageService.CurrentSessionPin))
            {
                var pinDlg = new PinPromptDialog(
                    "Yedeğinizi PIN kodunuzla mühürlemek için lütfen güvenlik PIN kodunuzu girin.",
                    forExternalPinPrompt: false)
                {
                    Owner = this
                };
                if (pinDlg.ShowDialog() != true) return;
            }

            string? path = _vm.CreateManualBackup();
            if (path != null)
            {
                RefreshBackupList();
            }
        }

        private void BtnOpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = VaultBackupService.BackupDirectory;
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnBrowseBackupFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Remofix Kasa Yedeği Seç (.rfbak)",
                Filter = "Remofix Kasa Yedeği (*.rfbak)|*.rfbak|Tüm Dosyalar (*.*)|*.*",
                InitialDirectory = VaultBackupService.BackupDirectory
            };

            if (dlg.ShowDialog() == true)
            {
                ConfirmAndRestoreFile(dlg.FileName);
            }
        }

        private void BtnRestoreSelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgBackups.SelectedItem is VaultBackupInfo selected && selected.IsValid)
            {
                ConfirmAndRestoreFile(selected.FilePath);
            }
        }

        private void ConfirmAndRestoreFile(string backupPath)
        {
            var info = VaultBackupService.InspectBackupFile(backupPath);
            if (info == null || !info.IsValid)
            {
                MessageBox.Show(
                    "Seçilen dosya geçerli bir Remofix kasa yedeği değil veya bozulmuş.",
                    "Geçersiz Dosya",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            string? requiredPin = null;
            if (info.IsPinProtected && !info.IsUnlocked)
            {
                var pinDlg = new PinPromptDialog(
                    $"'{info.FileName}' yedeğini açmak için lütfen bu yedeğin güvenlik PIN kodunu girin.",
                    forExternalPinPrompt: true)
                {
                    Owner = this
                };

                if (pinDlg.ShowDialog() != true) return;
                requiredPin = pinDlg.EnteredPin;

                var verifiedRecords = VaultBackupService.RestoreFromBackup(backupPath, requiredPin);
                if (verifiedRecords == null)
                {
                    MessageBox.Show(
                        "Hatalı PIN kodu girdiniz!\n\nBu yedek dosyası sahibinin özel PIN kodu ile mühürlenmiştir. Doğru PIN girilmeden yedeğin içeriği açılamaz veya geri yüklenemez.",
                        "PIN Doğrulama Başarısız",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                info.TotalRecords = verifiedRecords.Count;
                info.UsableRecords = verifiedRecords.Count(r => !r.IsUsed);
                info.UsedRecords = verifiedRecords.Count(r => r.IsUsed);
                info.IsUnlocked = true;
            }

            var confirm = MessageBox.Show(
                $"Bu yedeği kasaya geri yüklemek istiyor musunuz?\n\n" +
                $"• Dosya: {info.FileName}\n" +
                $"• Yedek Tarihi: {info.DisplayDate}\n" +
                $"• Toplam IMEI: {info.TotalRecords} adet ({info.UsableRecords} Kullanılabilir, {info.UsedRecords} Kullanılan)\n" +
                (info.IsPinProtected ? "• Koruma Düzeyi: 🔒 Kullanıcı PIN Kodu ile Mühürlü\n\n" : "\n") +
                $"Not: Mevcut kasanız güvenlik amacıyla işlem öncesinde otomatik olarak yedeklenecektir.",
                "Yedekten Geri Yükleme Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                bool success = _vm.RestoreVaultFromBackupFile(backupPath, requiredPin);
                if (success)
                {
                    WasVaultRestored = true;
                    RefreshBackupList();
                    MessageBox.Show(
                        $"{info.TotalRecords} adet IMEI kaydı başarıyla geri yüklendi.",
                        "Geri Yükleme Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
