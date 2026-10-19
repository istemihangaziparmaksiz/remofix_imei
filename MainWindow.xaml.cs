using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ImeiStudio.Models;
using ImeiStudio.Services;
using ImeiStudio.ViewModels;

namespace ImeiStudio
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _isFormattingBulk = false;
        private DispatcherTimer? _toastTimer;
        private bool _isVaultUnlockedThisSession = false;
        private string _activePanel = "SingleQuery";

        public MainWindow()
        {
            bool wasResetAtStartup = false;

            // Uygulama açılışında PIN kilidi aktifse pencere açılmadan önce sor
            var pkg = EmbeddedStorageService.LoadPackage();
            if (pkg.PinLockOnStartup && EmbeddedStorageService.HasPinConfigured())
            {
                var pinPrompt = new PinPromptDialog(
                    "Uygulamayı başlatmak için lütfen güvenlik PIN kodunuzu girin.");
                if (pinPrompt.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }
                wasResetAtStartup = pinPrompt.WasPinAndDataReset;
            }

            _vm = new MainViewModel();
            if (wasResetAtStartup)
            {
                _vm.WipeAllVaultRecords();
                _isVaultUnlockedThisSession = true;
            }

            InitializeComponent();
            DataContext = _vm;

            // ViewModel bildirimlerini modern toast ekranına bağla
            _vm.NotificationRequested += ShowToast;

            // Tema durumunu başlat ve dinle
            ThemeService.ThemeChanged += UpdateThemeButtonUi;
            UpdateThemeButtonUi(ThemeService.IsDarkTheme);
            UpdatePinStatusBadgeUi();

            if (wasResetAtStartup)
            {
                Loaded += (s, e) => ShowToast(
                    "Veriler ve PIN Sıfırlandı",
                    "Windows doğrulaması tamamlandı. Tüm kasa verileri ve PIN kilidi kaldırıldı.",
                    ToastType.Warning);
            }

            Closed += (s, e) => ImeiStudio.Services.EngineProcessManager.StopEngine();
        }

        #region Modern Toast Notification System

        public void ShowToast(string title, string message, ToastType type = ToastType.Success)
        {
            Dispatcher.Invoke(() =>
            {
                txtToastTitle.Text = title;
                txtToastMessage.Text = message;

                switch (type)
                {
                    case ToastType.Success:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(5, 46, 22));
                        toastIconBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(6, 95, 70));
                        txtToastIcon.Text = "✓";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                        break;
                    case ToastType.Info:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(24, 24, 28));
                        toastIconBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                        txtToastIcon.Text = "i";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                        break;
                    case ToastType.Warning:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(41, 21, 3));
                        toastIconBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 53, 15));
                        txtToastIcon.Text = "!";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                        break;
                    case ToastType.Danger:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(42, 10, 14));
                        toastIconBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                        txtToastIcon.Text = "✕";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                        break;
                }

                toastBorder.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
                toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                // Otomatik kapanma zamanlayıcısı (3 saniye)
                _toastTimer?.Stop();
                _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3000) };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();
                    HideToast();
                };
                _toastTimer.Start();
            });
        }

        private void HideToast()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                toastBorder.Visibility = Visibility.Collapsed;
            };
            toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void ToastClose_Click(object sender, RoutedEventArgs e)
        {
            _toastTimer?.Stop();
            HideToast();
        }

        #endregion

        #region Navigation & PIN Lock
        private void Nav_SingleQuery_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("SingleQuery", "Tekli IMEI Sorgulama", "14 haneli IMEI girin; 15. Luhn hanesi otomatik hesaplanıp birleştirilerek sorgulanır.");
        }

        private void Nav_BulkQuery_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("BulkQuery", "Toplu IMEI Sorgulama", "Listelenen IMEI'leri tek tek e-Devlet üzerinden seri olarak sorgulayın.");
        }

        private void Nav_Generator_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("Generator", "IMEI Üretici", "14 haneli başlangıç IMEI numarasına göre Luhn algoritmasıyla geçerli IMEI serileri üretin.");
        }

        private void Nav_Vault_Click(object sender, RoutedEventArgs e)
        {
            var pkg = EmbeddedStorageService.LoadPackage();
            if (pkg.PinLockVaultTab && EmbeddedStorageService.HasPinConfigured() && !_isVaultUnlockedThisSession)
            {
                var pinDlg = new PinPromptDialog(
                    "IMEI Kasası sekmesine erişmek için güvenlik PIN kodunuzu girin.")
                {
                    Owner = this
                };

                if (pinDlg.ShowDialog() != true)
                {
                    RestoreActiveNavRadioButton();
                    return;
                }

                if (pinDlg.WasPinAndDataReset)
                {
                    _vm.WipeAllVaultRecords();
                    UpdatePinStatusBadgeUi();
                    ShowToast(
                        "Veriler ve PIN Sıfırlandı",
                        "Windows doğrulaması tamamlandı. Tüm kasa verileri ve PIN kilidi kaldırıldı.",
                        ToastType.Warning);
                }

                _isVaultUnlockedThisSession = true;
            }

            ShowPanel("Vault", "IMEI Kasası", "Kayıtlı IMEI numaralarını kullanılabilir ve kullanılanlar olarak yönetin.");
        }

        private void RestoreActiveNavRadioButton()
        {
            switch (_activePanel)
            {
                case "SingleQuery":
                    if (rbNavSingle != null) rbNavSingle.IsChecked = true;
                    break;
                case "BulkQuery":
                    if (rbNavBulk != null) rbNavBulk.IsChecked = true;
                    break;
                case "Generator":
                    if (rbNavGen != null) rbNavGen.IsChecked = true;
                    break;
                case "Vault":
                    if (rbNavVault != null) rbNavVault.IsChecked = true;
                    break;
            }
        }

        private void ShowPanel(string panelName, string title, string subtitle)
        {
            if (txtPageTitle == null) return;
            _activePanel = panelName;
            txtPageTitle.Text = title;
            txtPageSubtitle.Text = subtitle;

            panelSingleQuery.Visibility = panelName == "SingleQuery" ? Visibility.Visible : Visibility.Collapsed;
            panelBulkQuery.Visibility = panelName == "BulkQuery" ? Visibility.Visible : Visibility.Collapsed;
            panelGenerator.Visibility = panelName == "Generator" ? Visibility.Visible : Visibility.Collapsed;
            panelVault.Visibility = panelName == "Vault" ? Visibility.Visible : Visibility.Collapsed;

            RestoreActiveNavRadioButton();
        }

        private void BtnPinSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PinSettingsDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                if (dlg.WasPinAndDataReset)
                {
                    _vm.WipeAllVaultRecords();
                    _isVaultUnlockedThisSession = true;
                    UpdatePinStatusBadgeUi();
                    ShowToast(
                        "Veriler ve PIN Sıfırlandı",
                        "Windows doğrulaması tamamlandı. Tüm kasa verileri ve PIN kilidi kaldırıldı.",
                        ToastType.Warning);
                    return;
                }

                _isVaultUnlockedThisSession = false;
                UpdatePinStatusBadgeUi();
                bool hasPin = EmbeddedStorageService.HasPinConfigured();
                ShowToast(
                    hasPin ? "PIN Kilidi Güncellendi" : "PIN Kilidi Kaldırıldı",
                    hasPin ? "Güvenlik PIN ayarlarınız şifrelenerek kaydedildi." : "PIN koruması devre dışı bırakıldı.",
                    hasPin ? ToastType.Success : ToastType.Info);
            }
        }

        private void UpdatePinStatusBadgeUi()
        {
            if (txtPinStatusBadge == null || borderPinStatusBadge == null) return;
            var pkg = EmbeddedStorageService.LoadPackage();
            bool active = EmbeddedStorageService.HasPinConfigured() && (pkg.PinLockOnStartup || pkg.PinLockVaultTab);

            txtPinStatusBadge.Text = active ? "AKTİF" : "KAPALI";
            txtPinStatusBadge.Foreground = active
                ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                : (Brush)Application.Current.Resources["TextMutedBrush"];
        }
        #endregion

        #region Input Restrictions & Auto-Newline

        // Tekli & Üretici için sadece rakam kontrolü
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        // Toplu giriş için sadece rakam kontrolü (Enter ve Backspace izinli)
        private void BulkNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[\d\r\n]+$");
        }

        // Toplu girişte her 15 hanede bir kullanıcının atlamasına gerek kalmadan otomatik alt satıra geçme
        private void BulkInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingBulk) return;

            string current = txtBulkInput.Text;
            if (string.IsNullOrEmpty(current)) return;

            string normalized = current.Replace("\r\n", "\n").Replace("\r", "\n");
            var rawLines = normalized.Split('\n');
            bool hasOverflow = rawLines.Any(l => Regex.Replace(l, @"\D", "").Length > 15);

            if (hasOverflow)
            {
                _isFormattingBulk = true;
                string allDigits = Regex.Replace(current, @"\D", "");
                var chunks = new System.Collections.Generic.List<string>();
                for (int i = 0; i < allDigits.Length; i += 15)
                {
                    int len = System.Math.Min(15, allDigits.Length - i);
                    chunks.Add(allDigits.Substring(i, len));
                }

                txtBulkInput.Text = string.Join(System.Environment.NewLine, chunks);
                txtBulkInput.CaretIndex = txtBulkInput.Text.Length;
                _isFormattingBulk = false;
            }
        }

        #endregion

        #region Single Query Actions
        private async void BtnQuerySingle_Click(object sender, RoutedEventArgs e)
        {
            await _vm.ExecuteSingleQueryAsync();
        }

        private async void SingleImeiTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await _vm.ExecuteSingleQueryAsync();
            }
        }

        private void BtnPasteSingle_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText().Trim();
                string digits = Regex.Replace(text, @"\D", "");
                if (digits.Length >= 14)
                {
                    _vm.SingleImei14 = digits.Substring(0, 14);
                }
                else
                {
                    _vm.SingleImei14 = digits;
                }
            }
        }

        private void BtnCopyImei_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.LastSingleResult != null && !string.IsNullOrEmpty(_vm.LastSingleResult.Imei))
            {
                Clipboard.SetText(_vm.LastSingleResult.Imei);
                ShowToast("Panoya Kopyalandı", $"{_vm.LastSingleResult.Imei} IMEI numarası panoya kopyalandı.", ToastType.Info);
            }
        }

        private void BtnSaveSingleToVault_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.LastSingleResult != null)
            {
                _vm.AddOrUpdateVault(_vm.LastSingleResult);
                ShowToast("Kasaya Kaydedildi", $"{_vm.LastSingleResult.Imei} IMEI Kasasına başarıyla kaydedildi.", ToastType.Success);
            }
        }
        #endregion

        #region Bulk Query Actions
        private async void BtnStartBulk_Click(object sender, RoutedEventArgs e)
        {
            await _vm.ExecuteBulkQueryAsync();
        }

        private void BtnPauseBulk_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleBulkPause();
        }

        private void BtnStopBulk_Click(object sender, RoutedEventArgs e)
        {
            _vm.StopBulkQuery();
        }

        private void BtnClearBulk_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.IsBulkQueryBusy)
            {
                ShowToast("İşlem Devam Ediyor", "Temizlemeden önce lütfen devam eden sorguyu durdurun.", ToastType.Warning);
                return;
            }
            _vm.BulkInputText = string.Empty;
            _vm.BulkResults.Clear();
            _vm.BulkStatusMessage = "Hazır";
            _vm.BulkProgressPercentage = 0;
            _vm.IsBulkFinished = false;
        }

        private void BtnSaveRegisteredBulk_Click(object sender, RoutedEventArgs e)
        {
            _vm.SaveBulkRegisteredToVault();
        }
        #endregion

        #region Generator & TAC Presets Actions
        private void BtnGenerateImeis_Click(object sender, RoutedEventArgs e)
        {
            _vm.ExecuteGenerateImeis();
        }

        private void BtnSendToBulk_Click(object sender, RoutedEventArgs e)
        {
            _vm.TransferGeneratedToBulk();
            ShowPanel("BulkQuery", "Toplu IMEI Sorgulama", "Üretilen IMEI serisi toplu sorguya yüklendi.");
        }

        private void GenCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && _vm != null)
            {
                if (int.TryParse(item.Tag?.ToString(), out int c))
                {
                    _vm.GenCount = c;
                }
            }
        }

        private void GenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && _vm != null)
            {
                _vm.GenMode = item.Tag?.ToString() ?? "sequential";
            }
        }

        private void BtnAddTacPreset_Click(object sender, RoutedEventArgs e)
        {
            _vm.AddCurrentTacToPresets();
        }

        private void TacPreset_Select_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TacPreset preset)
            {
                _vm.GenPrefix = preset.Prefix;
                _vm.GenLogMessage = $"'{preset.Name}' modelinin TAC prefix kodu ({preset.Prefix}) yüklendi.";
                ShowToast("TAC Prefix Seçildi", $"{preset.Name} ({preset.Prefix}) üretim alanına aktarıldı.", ToastType.Info);
            }
        }

        private void TacPreset_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TacPreset preset)
            {
                _vm.RemoveTacPreset(preset);
            }
        }
        #endregion

        #region Vault Actions (Kullanılabilir vs Kullanılanlar, Çoklu Seçim, Çift SIM, Manuel Ekleme)

        private void SubTab_Usable_Click(object sender, RoutedEventArgs e)
        {
            _vm.VaultSubTab = "Usable";
            borderUsableVault.Visibility = Visibility.Visible;
            borderUsedVault.Visibility = Visibility.Collapsed;
            _vm.RefreshSelectionStats();
        }

        private void SubTab_Used_Click(object sender, RoutedEventArgs e)
        {
            _vm.VaultSubTab = "Used";
            borderUsableVault.Visibility = Visibility.Collapsed;
            borderUsedVault.Visibility = Visibility.Visible;
            _vm.RefreshSelectionStats();
        }

        private void VaultSelectionDot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                rec.IsSelected = !rec.IsSelected;
                _vm.RefreshSelectionStats();
            }
        }

        private void BtnToggleSelectAllVault_Click(object sender, RoutedEventArgs e)
        {
            var activeList = _vm.VaultSubTab == "Usable" ? _vm.UsableRecords : _vm.UsedRecords;
            if (!activeList.Any()) return;

            bool allSelected = activeList.All(r => r.IsSelected);
            _vm.SetAllCurrentTabSelection(!allSelected);
        }

        private void BtnManualAddVault_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ManualAddDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.CreatedRecord != null)
            {
                _vm.AddOrUpdateVault(dlg.CreatedRecord);
                ShowToast("Manuel IMEI Eklendi", $"{dlg.CreatedRecord.Imei} sorgusuz olarak kasaya kaydedildi.", ToastType.Success);
            }
        }

        private void BtnPairDualSim_Click(object sender, RoutedEventArgs e)
        {
            var selected = _vm.UsableRecords.Where(r => r.IsSelected).ToList();
            if (selected.Count != 2)
            {
                ShowToast("2 Adet IMEI Seçin", "Çift SIM eşleştirmesi için Kullanılabilir listeden tam 2 adet IMEI seçmelisiniz.", ToastType.Warning);
                return;
            }

            var dlg = new MarkUsedDialog(selected[0], selected[1]) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _vm.MarkDualSimAsUsed(selected[0], selected[1], dlg.CustomerText, dlg.TargetDeviceText, dlg.NoteText);
                ShowToast(
                    "Çift SIM Eşleştirildi",
                    $"SIM 1 ({selected[0].Imei}) ve SIM 2 ({selected[1].Imei}) eşleştirilerek Kullanılanlar'a taşındı.",
                    ToastType.Success);
            }
        }

        private void BtnCopySelectedVault_Click(object sender, RoutedEventArgs e)
        {
            _vm.CopySelectedVaultImeis();
        }

        private void BtnDeleteSelectedVault_Click(object sender, RoutedEventArgs e)
        {
            int count = _vm.SelectedVaultCount;
            if (count == 0) return;

            var dummy = new ImeiRecord
            {
                Imei = $"Seçili {count} Adet IMEI Kaydı",
                BrandModel = "Toplu Silme İşlemi"
            };
            var confirm = new ConfirmDeleteDialog(dummy) { Owner = this };
            if (confirm.ShowDialog() == true)
            {
                int deleted = _vm.DeleteSelectedVaultRecords();
                ShowToast("Seçili Kayıtlar Silindi", $"Seçilen {deleted} adet IMEI kaydı kasadan silindi.", ToastType.Danger);
            }
        }

        private void VaultAction_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                Clipboard.SetText(rec.Imei);
                ShowToast("Panoya Kopyalandı", $"{rec.Imei} IMEI numarası panoya kopyalandı.", ToastType.Info);
            }
        }

        // KULLANILDI BUTONUNA TIKLANINCA: DETAYLI KULLANIM KAYDI PENCERESİ AÇILIR
        private void VaultAction_MarkUsed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                var dlg = new MarkUsedDialog(rec) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    _vm.MarkAsUsedDetailed(rec, dlg.CustomerText, dlg.TargetDeviceText, dlg.NoteText);
                    ShowToast("Kullanılanlara Taşındı", $"{rec.Imei} kullanım detaylarıyla birlikte 'Kullanılanlar' kasasına aktarıldı.", ToastType.Success);
                }
            }
        }

        private void VaultAction_Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                string? pairedImei = _vm.RestoreToUsable(rec);
                if (!string.IsNullOrEmpty(pairedImei))
                {
                    ShowToast(
                        "Çift SIM Geri Yüklendi",
                        $"{rec.Imei} ve eşleşen {pairedImei} birlikte 'Kullanılabilir' kasasına geri alındı.",
                        ToastType.Info);
                }
                else
                {
                    ShowToast("Geri Yüklendi", $"{rec.Imei} tekrar 'Kullanılabilir' kasasına geri alındı.", ToastType.Info);
                }
            }
        }

        private void VaultAction_AddNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                var dlg = new NoteDialog(rec) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    _vm.UpdateUsageDetails(rec, dlg.CustomerText, dlg.TargetDeviceText, dlg.NoteText);
                    ShowToast("Kayıt Güncellendi", $"{rec.Imei} için müşteri, cihaz ve not bilgileri güncellendi.", ToastType.Success);
                }
            }
        }

        // SİL BUTONUNA TIKLANINCA: MODERN ONAY PENCERESİ AÇILIR
        private void VaultAction_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                var confirm = new ConfirmDeleteDialog(rec) { Owner = this };
                if (confirm.ShowDialog() == true)
                {
                    _vm.DeleteVaultRecord(rec);
                    ShowToast("Kayıt Silindi", $"{rec.Imei} IMEI numarası kasadan silindi.", ToastType.Danger);
                }
            }
        }

        private void BtnPauseVaultCheck_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleVaultCheckPause();
        }

        private void BtnStopVaultCheck_Click(object sender, RoutedEventArgs e)
        {
            _vm.StopVaultCheck();
        }

        private async void BtnCheckVault_Click(object sender, RoutedEventArgs e)
        {
            bool isUsedTab = _vm.VaultSubTab == "Used";

            var summary = await _vm.ExecuteVaultCheckAsync(rec =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (!isUsedTab && dgUsableVault != null && dgUsableVault.IsLoaded && dgUsableVault.Items.Contains(rec))
                        {
                            dgUsableVault.ScrollIntoView(rec);
                        }
                        else if (isUsedTab && dgUsedVault != null && dgUsedVault.IsLoaded && dgUsedVault.Items.Contains(rec))
                        {
                            dgUsedVault.ScrollIntoView(rec);
                        }
                    }
                    catch { }
                }, DispatcherPriority.Background);
            });

            if (summary != null)
            {
                var dlg = new VaultCheckResultDialog(summary) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.KeepOnlyRegistered)
                {
                    int removedCount = _vm.KeepOnlyRegisteredInCurrentTab();
                    ShowToast(
                        "Kasa Temizlendi",
                        $"Kayıt dışı / klonlanmış {removedCount} adet IMEI kasadan kaldırıldı. {summary.RegisteredCount} adet kayıtlı IMEI korundu.",
                        ToastType.Success);
                }
                else
                {
                    ShowToast(
                        "Kontrol Tamamlandı",
                        $"Toplam {summary.TotalChecked} IMEI kontrol edildi ve mevcut liste olduğu gibi bırakıldı.",
                        ToastType.Info);
                }
            }
        }

        private void BtnImportVault_Click(object sender, RoutedEventArgs e)
        {
            _vm.ImportVaultFromTxt();
        }

        private void BtnExportVault_Click(object sender, RoutedEventArgs e)
        {
            _vm.ExportCurrentVaultToTxt();
        }
        #endregion

        #region Theme System
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.ToggleTheme();
        }

        private void UpdateThemeButtonUi(bool isDark)
        {
            Dispatcher.Invoke(() =>
            {
                if (pathThemeIcon != null && txtThemeTitle != null && borderToggleTrack != null && toggleThumb != null)
                {
                    var geoKey = isDark ? "GeoMoon" : "GeoSun";
                    if (Application.Current.Resources[geoKey] is Geometry g)
                    {
                        pathThemeIcon.Data = g;
                    }

                    txtThemeTitle.Text = isDark ? "Koyu Görünüm" : "Açık Görünüm";

                    if (isDark)
                    {
                        borderToggleTrack.Background = new SolidColorBrush(Color.FromRgb(39, 39, 42));
                        borderToggleTrack.BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                        toggleThumb.Fill = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                        toggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
                    }
                    else
                    {
                        borderToggleTrack.Background = new SolidColorBrush(Color.FromRgb(228, 228, 231));
                        borderToggleTrack.BorderBrush = new SolidColorBrush(Color.FromRgb(212, 212, 216));
                        toggleThumb.Fill = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                        toggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
                    }
                }
            });
        }
        #endregion
    }
}