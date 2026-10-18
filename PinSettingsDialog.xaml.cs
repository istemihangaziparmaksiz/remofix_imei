using System.Windows;
using ImeiStudio.Services;

namespace ImeiStudio
{
    public partial class PinSettingsDialog : Window
    {
        public string StatusMessage { get; private set; } = string.Empty;
        public bool WasPinAndDataReset { get; private set; } = false;

        public PinSettingsDialog()
        {
            InitializeComponent();
            var pkg = EmbeddedStorageService.LoadPackage();
            bool hasPin = !string.IsNullOrEmpty(pkg.PinHash);

            panelCurrentPin.Visibility = hasPin ? Visibility.Visible : Visibility.Collapsed;
            btnRemovePin.Visibility = hasPin ? Visibility.Visible : Visibility.Collapsed;

            chkLockVault.IsChecked = pkg.PinLockVaultTab;
            chkLockStartup.IsChecked = pkg.PinLockOnStartup;
        }

        private void BtnForgotPinInSettings_Click(object sender, RoutedEventArgs e)
        {
            var forgotDlg = new PinPromptDialog(
                "PIN sıfırlama işlemi güvenlik nedeniyle tüm kasa verilerinizi siler.",
                startInForgotMode: true)
            {
                Owner = this
            };

            if (forgotDlg.ShowDialog() == true && forgotDlg.WasPinAndDataReset)
            {
                WasPinAndDataReset = true;
                StatusMessage = "Windows doğrulaması tamamlandı. Tüm kasa verileri ve PIN kilidi sıfırlandı.";
                DialogResult = true;
                Close();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            bool hasExistingPin = !string.IsNullOrEmpty(pkg.PinHash);

            if (hasExistingPin)
            {
                if (!EmbeddedStorageService.VerifyPin(pbCurrentPin.Password))
                {
                    txtError.Text = "Mevcut PIN kodunu hatalı girdiniz.";
                    txtError.Visibility = Visibility.Visible;
                    return;
                }
            }

            string newPin = (pbNewPin.Password ?? "").Trim();
            if (string.IsNullOrEmpty(newPin) && !hasExistingPin)
            {
                txtError.Text = "Lütfen en az 3 karakterli yeni bir PIN veya parola belirleyin.";
                txtError.Visibility = Visibility.Visible;
                return;
            }

            if (!string.IsNullOrEmpty(newPin))
            {
                pkg.PinHash = EmbeddedStorageService.ComputePinHash(newPin);
            }

            pkg.PinLockVaultTab = chkLockVault.IsChecked == true;
            pkg.PinLockOnStartup = chkLockStartup.IsChecked == true;
            EmbeddedStorageService.SavePackage(pkg);

            StatusMessage = "PIN kilidi ve güvenlik tercihleri başarıyla kaydedildi.";
            DialogResult = true;
            Close();
        }

        private void BtnRemovePin_Click(object sender, RoutedEventArgs e)
        {
            if (!EmbeddedStorageService.VerifyPin(pbCurrentPin.Password))
            {
                txtError.Text = "PIN kilidini kaldırmak için mevcut PIN kodunu doğru girmelisiniz.";
                txtError.Visibility = Visibility.Visible;
                return;
            }

            var pkg = EmbeddedStorageService.LoadPackage();
            pkg.PinHash = null;
            pkg.PinLockOnStartup = false;
            pkg.PinLockVaultTab = false;
            EmbeddedStorageService.SavePackage(pkg);

            StatusMessage = "PIN kilidi tamamen kaldırıldı.";
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
