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

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            // ViewModel bildirimlerini modern toast ekranına bağla
            _vm.NotificationRequested += ShowToast;

            // Tema durumunu başlat ve dinle
            ThemeService.ThemeChanged += UpdateThemeButtonUi;
            UpdateThemeButtonUi(ThemeService.IsDarkTheme);

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
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                        txtToastIcon.Text = "✓";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                        break;
                    case ToastType.Info:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        txtToastIcon.Text = "ℹ";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                        break;
                    case ToastType.Warning:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(120, 53, 15));
                        txtToastIcon.Text = "⚠";
                        txtToastIcon.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                        break;
                    case ToastType.Danger:
                        toastIconBorder.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                        txtToastIcon.Text = "🗑";
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

        #region Navigation
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
            ShowPanel("Generator", "IMEI Üretici", "Belirtilen TAC prefix'ine göre Luhn algoritmasıyla geçerli IMEI serileri üretin.");
        }

        private void Nav_Vault_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("Vault", "IMEI Kasası", "Kayıtlı IMEI numaralarını kullanılabilir ve kullanılanlar olarak yönetin.");
        }

        private void ShowPanel(string panelName, string title, string subtitle)
        {
            if (txtPageTitle == null) return;
            txtPageTitle.Text = title;
            txtPageSubtitle.Text = subtitle;

            panelSingleQuery.Visibility = panelName == "SingleQuery" ? Visibility.Visible : Visibility.Collapsed;
            panelBulkQuery.Visibility = panelName == "BulkQuery" ? Visibility.Visible : Visibility.Collapsed;
            panelGenerator.Visibility = panelName == "Generator" ? Visibility.Visible : Visibility.Collapsed;
            panelVault.Visibility = panelName == "Vault" ? Visibility.Visible : Visibility.Collapsed;
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

        private void BtnClearBulk_Click(object sender, RoutedEventArgs e)
        {
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

        #region Generator Actions
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
        #endregion

        #region Vault Actions (Kullanılabilir vs Kullanılanlar)

        private void SubTab_Usable_Click(object sender, RoutedEventArgs e)
        {
            _vm.VaultSubTab = "Usable";
            borderUsableVault.Visibility = Visibility.Visible;
            borderUsedVault.Visibility = Visibility.Collapsed;
        }

        private void SubTab_Used_Click(object sender, RoutedEventArgs e)
        {
            _vm.VaultSubTab = "Used";
            borderUsableVault.Visibility = Visibility.Collapsed;
            borderUsedVault.Visibility = Visibility.Visible;
        }

        private void VaultAction_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                Clipboard.SetText(rec.Imei);
                ShowToast("Panoya Kopyalandı", $"{rec.Imei} IMEI numarası panoya kopyalandı.", ToastType.Info);
            }
        }

        // KULLANILDI BUTONUNA TIKLANINCA: NOT EKLEME EKRANI AÇILIR
        private void VaultAction_MarkUsed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                var dlg = new MarkUsedDialog(rec) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    rec.Note = dlg.NoteText;
                    _vm.MarkAsUsed(rec);
                    ShowToast("Kullanılanlara Taşındı", $"{rec.Imei} notuyla birlikte 'Kullanılanlar' kasasına aktarıldı.", ToastType.Success);
                }
            }
        }

        private void VaultAction_Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                _vm.RestoreToUsable(rec);
                ShowToast("Geri Yüklendi", $"{rec.Imei} tekrar 'Kullanılabilir' kasasına geri alındı.", ToastType.Info);
            }
        }

        private void VaultAction_AddNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ImeiRecord rec)
            {
                var dlg = new NoteDialog(rec.Note) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    _vm.AddNoteToRecord(rec, dlg.NoteText);
                    ShowToast("Not Güncellendi", $"{rec.Imei} için özel not kaydedildi.", ToastType.Success);
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
                if (txtThemeIcon != null && txtThemeTitle != null && borderToggleTrack != null && toggleThumb != null)
                {
                    txtThemeIcon.Text = isDark ? "🌙" : "☀️";
                    txtThemeTitle.Text = isDark ? "Koyu Tema" : "Açık Tema";

                    if (isDark)
                    {
                        borderToggleTrack.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB Mavi
                        toggleThumb.HorizontalAlignment = HorizontalAlignment.Right;
                    }
                    else
                    {
                        borderToggleTrack.Background = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #CBD5E1 Açık gri
                        toggleThumb.HorizontalAlignment = HorizontalAlignment.Left;
                    }
                }
            });
        }
        #endregion
    }
}