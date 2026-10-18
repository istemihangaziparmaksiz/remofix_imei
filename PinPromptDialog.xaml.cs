using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ImeiStudio.Services;

namespace ImeiStudio
{
    public partial class PinPromptDialog : Window
    {
        private readonly string _initialSubtitle;
        private DispatcherTimer? _countdownTimer;
        private int _resetStage = 0; // 1 = İlk 10 sn uyarısı, 2 = İkinci 10 sn uyarısı
        private int _remainingSeconds = 10;

        /// <summary>
        /// Kullanıcı "Şifremi Unuttum" adımlarını ve Windows doğrulamasını tamamlayarak
        /// tüm verileri ve PIN kilidini sıfırladıysa true döner.
        /// </summary>
        public bool WasPinAndDataReset { get; private set; } = false;

        public PinPromptDialog(string? subtitle = null, bool startInForgotMode = false)
        {
            InitializeComponent();
            _initialSubtitle = string.IsNullOrEmpty(subtitle)
                ? "Devam etmek için belirlediğiniz güvenlik PIN / şifresini girin."
                : subtitle;

            txtPromptSubtitle.Text = _initialSubtitle;

            Closed += (s, e) => StopCountdownTimer();

            if (startInForgotMode)
            {
                Loaded += (s, e) => StartForgotResetStage1();
            }
            else
            {
                Loaded += (s, e) => pbPin.Focus();
            }
        }

        private void PbPin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                VerifyAndUnlock();
            }
        }

        private void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            VerifyAndUnlock();
        }

        private void VerifyAndUnlock()
        {
            string input = pbPin.Password ?? string.Empty;
            if (EmbeddedStorageService.VerifyPin(input))
            {
                WasPinAndDataReset = false;
                DialogResult = true;
                Close();
            }
            else
            {
                txtError.Visibility = Visibility.Visible;
                pbPin.Password = string.Empty;
                pbPin.Focus();
            }
        }

        private void BtnForgotPin_Click(object sender, RoutedEventArgs e)
        {
            StartForgotResetStage1();
        }

        private void BtnBackToPin_Click(object sender, RoutedEventArgs e)
        {
            StopCountdownTimer();
            _resetStage = 0;

            txtPromptTitle.Text = "PIN Kilidi Koruması";
            txtPromptSubtitle.Text = _initialSubtitle;
            pathHeaderIcon.Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129));

            panelForgotReset.Visibility = Visibility.Collapsed;
            panelResetButtons.Visibility = Visibility.Collapsed;

            panelPinEntry.Visibility = Visibility.Visible;
            panelPinButtons.Visibility = Visibility.Visible;
            pbPin.Focus();
        }

        private void StartForgotResetStage1()
        {
            _resetStage = 1;
            txtResetError.Visibility = Visibility.Collapsed;

            txtPromptTitle.Text = "Şifremi Unuttum — Veri Sıfırlama";
            txtPromptSubtitle.Text = "PIN sıfırlama işlemi güvenlik gereği tüm kasa verilerini siler.";
            pathHeaderIcon.Stroke = new SolidColorBrush(Color.FromRgb(248, 113, 113));

            panelPinEntry.Visibility = Visibility.Collapsed;
            panelPinButtons.Visibility = Visibility.Collapsed;

            panelForgotReset.Visibility = Visibility.Visible;
            panelResetButtons.Visibility = Visibility.Visible;

            txtResetStepBadge.Text = "ONAY 1 / 2";
            txtResetStageTitle.Text = "DİKKAT: TÜM VERİLER SİLİNECEKTİR";
            txtResetWarningBody.Text =
                "PIN kodunuzu unuttuysanız güvenlik nedeniyle şifre tek başına kaldırılamaz. " +
                "Devam etmeniz halinde IMEI Kasasındaki tüm Kullanılabilir ve Kullanılan IMEI kayıtlarınız " +
                "kalıcı olarak silinecek ve PIN kilidi sıfırlanacaktır.";

            StartTenSecondCountdown("Lütfen Bekleyin", "Onaylıyorum, Devam Et (1/2)");
        }

        private void StartForgotResetStage2()
        {
            _resetStage = 2;
            txtResetError.Visibility = Visibility.Collapsed;

            txtResetStepBadge.Text = "SON ONAY 2 / 2";
            txtResetStageTitle.Text = "SON UYARI: BU İŞLEM GERİ ALINAMAZ!";
            txtResetWarningBody.Text =
                "İkinci ve son onay adımındasınız! Bu adımdan sonra kasadaki tüm IMEI numaraları, " +
                "müşteri/cihaz notları ve PIN şifreniz tamamen silinecektir.\n\n" +
                "Yetkisiz kişilerin verilerinizi silmesini engellemek için son adımda bilgisayarınızın " +
                "Windows PIN / Parola doğrulaması istenecektir.";

            StartTenSecondCountdown("Son Onay İçin Bekleyin", "Windows Doğrulaması ile Sıfırla (2/2)");
        }

        private void StartTenSecondCountdown(string waitingPrefix, string readyButtonText)
        {
            StopCountdownTimer();

            _remainingSeconds = 10;
            pbCountdown.Minimum = 0;
            pbCountdown.Maximum = 10;
            pbCountdown.Value = 0;

            txtCountdownStatus.Text = _resetStage == 1
                ? "1. güvenlik bekleme süresi devam ediyor..."
                : "2. ve son güvenlik bekleme süresi devam ediyor...";
            txtCountdownSeconds.Text = $"{_remainingSeconds} sn";

            btnConfirmResetStep.IsEnabled = false;
            btnConfirmResetStep.Content = $"{waitingPrefix} ({_remainingSeconds} sn)...";

            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _countdownTimer.Tick += (s, e) =>
            {
                _remainingSeconds--;
                pbCountdown.Value = 10 - _remainingSeconds;

                if (_remainingSeconds > 0)
                {
                    txtCountdownSeconds.Text = $"{_remainingSeconds} sn";
                    btnConfirmResetStep.Content = $"{waitingPrefix} ({_remainingSeconds} sn)...";
                }
                else
                {
                    StopCountdownTimer();
                    pbCountdown.Value = 10;
                    txtCountdownSeconds.Text = "Hazır";
                    txtCountdownStatus.Text = "Bekleme süresi tamamlandı. Onay verebilirsiniz.";
                    btnConfirmResetStep.Content = readyButtonText;
                    btnConfirmResetStep.IsEnabled = true;
                }
            };

            _countdownTimer.Start();
        }

        private void StopCountdownTimer()
        {
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer = null;
            }
        }

        private void BtnConfirmResetStep_Click(object sender, RoutedEventArgs e)
        {
            if (_remainingSeconds > 0) return;

            if (_resetStage == 1)
            {
                // 1. 10 saniye bitti ve kullanıcı onayladı -> 2. 10 saniyelik onayı başlat
                StartForgotResetStage2();
                return;
            }

            if (_resetStage == 2)
            {
                // 2. 10 saniye de bitti -> Bilgisayarın Windows Güvenliği (PIN/Şifre) doğrulamasını iste
                txtResetError.Visibility = Visibility.Collapsed;

                if (!WindowsAuthService.VerifyCurrentWindowsUser(this, out string authError))
                {
                    txtResetError.Text = string.IsNullOrEmpty(authError)
                        ? "Windows güvenlik doğrulaması başarısız oldu. Veriler silinmedi."
                        : $"{authError} Verileriniz silinmedi.";
                    txtResetError.Visibility = Visibility.Visible;
                    return;
                }

                // Windows doğrulaması başarılı -> Tüm verileri ve PIN'i sil
                EmbeddedStorageService.ResetAllVaultAndPin();
                WasPinAndDataReset = true;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            StopCountdownTimer();
            DialogResult = false;
            Close();
        }
    }
}
