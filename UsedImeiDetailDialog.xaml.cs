using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ImeiStudio.Models;

namespace ImeiStudio
{
    public partial class UsedImeiDetailDialog : Window
    {
        private readonly ImeiRecord _record;
        private readonly Action<ImeiRecord, string, string, string>? _onUpdateRequested;

        public bool WereDetailsModified { get; private set; } = false;

        public UsedImeiDetailDialog(ImeiRecord record, Action<ImeiRecord, string, string, string>? onUpdateRequested = null)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _onUpdateRequested = onUpdateRequested;

            InitializeComponent();
            RefreshUi();
        }

        private void RefreshUi()
        {
            txtImei.Text = _record.Imei;

            // Çift SIM Durumu
            if (_record.HasPairedImei)
            {
                badgeDualSim.Visibility = Visibility.Visible;
                borderPairedImei.Visibility = Visibility.Visible;
                txtPairedImei.Text = _record.PairedImei;
            }
            else
            {
                badgeDualSim.Visibility = Visibility.Collapsed;
                borderPairedImei.Visibility = Visibility.Collapsed;
            }

            // Donanım Modeli
            txtBrandModel.Text = string.IsNullOrWhiteSpace(_record.BrandModel) || _record.BrandModel == "-"
                ? "Donanım bilgisi belirtilmemiş"
                : _record.BrandModel;

            // BTK Durumu ve Rozeti
            try
            {
                if (Resources["CatBgConv"] is IValueConverter catBg &&
                    Resources["CatBorderConv"] is IValueConverter catBorder &&
                    Resources["CatFgConv"] is IValueConverter catFg)
                {
                    badgeStatusCategory.Background = (Brush)catBg.Convert(_record.Category, typeof(Brush), null, CultureInfo.CurrentCulture);
                    badgeStatusCategory.BorderBrush = (Brush)catBorder.Convert(_record.Category, typeof(Brush), null, CultureInfo.CurrentCulture);
                    txtStatusCategory.Foreground = (Brush)catFg.Convert(_record.Category, typeof(Brush), null, CultureInfo.CurrentCulture);
                }
            }
            catch { }

            txtStatusCategory.Text = string.IsNullOrWhiteSpace(_record.Category) ? "BİLİNMİYOR" : _record.Category;
            txtStatusDetails.Text = string.IsNullOrWhiteSpace(_record.Status) ? "—" : _record.Status;

            // Müşteri Bilgisi
            txtCustomer.Text = string.IsNullOrWhiteSpace(_record.CustomerInfo) ? "Belirtilmemiş" : _record.CustomerInfo;

            // Takılan Cihaz
            txtTargetDevice.Text = string.IsNullOrWhiteSpace(_record.TargetDevice) ? "Belirtilmemiş" : _record.TargetDevice;

            // İşlem Tarihi
            txtUsedDate.Text = _record.EffectiveUsedDate.ToString("dd.MM.yyyy HH:mm:ss");

            // Servis Notu
            txtNote.Text = string.IsNullOrWhiteSpace(_record.Note) ? "Ek servis notu girilmemiş." : _record.Note;
        }

        private void BtnCopyImei_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_record.Imei);
                if (sender is System.Windows.Controls.Button btn)
                {
                    string oldText = btn.Content?.ToString() ?? "IMEI Kopyala";
                    btn.Content = "✓ Kopyalandı";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                    timer.Tick += (s, ev) => { btn.Content = oldText; timer.Stop(); };
                    timer.Start();
                }
            }
            catch { }
        }

        private void BtnCopyPairedImei_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_record.PairedImei))
                {
                    Clipboard.SetText(_record.PairedImei);
                    if (sender is System.Windows.Controls.Button btn)
                    {
                        string oldText = btn.Content?.ToString() ?? "2. IMEI Kopyala";
                        btn.Content = "✓ Kopyalandı";
                        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                        timer.Tick += (s, ev) => { btn.Content = oldText; timer.Stop(); };
                        timer.Start();
                    }
                }
            }
            catch { }
        }

        private void BtnCopyAllDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("📋 REMOFIX IMEI İŞLEM DETAYI");
                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine($"IMEI Numarası   : {_record.Imei}");
                if (_record.HasPairedImei)
                {
                    sb.AppendLine($"Çift SIM (2. IMEI): {_record.PairedImei}");
                }
                sb.AppendLine($"Kaynak Model    : {_record.BrandModel}");
                sb.AppendLine($"BTK Durumu      : {_record.Category} ({_record.Status})");
                sb.AppendLine($"Müşteri Bilgisi : {(!string.IsNullOrWhiteSpace(_record.CustomerInfo) ? _record.CustomerInfo : "—")}");
                sb.AppendLine($"İşlem Yapılan   : {(!string.IsNullOrWhiteSpace(_record.TargetDevice) ? _record.TargetDevice : "—")}");
                sb.AppendLine($"İşlem Tarihi    : {_record.EffectiveUsedDate:dd.MM.yyyy HH:mm:ss}");
                sb.AppendLine($"Servis Notu     : {(!string.IsNullOrWhiteSpace(_record.Note) ? _record.Note : "—")}");
                sb.AppendLine("--------------------------------------------------");

                Clipboard.SetText(sb.ToString());

                if (sender is System.Windows.Controls.Button btn)
                {
                    string oldText = btn.Content?.ToString() ?? "📋 Tüm Bilgileri Kopyala";
                    btn.Content = "✓ Bilgiler Panoya Kopyalandı!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                    timer.Tick += (s, ev) => { btn.Content = oldText; timer.Stop(); };
                    timer.Start();
                }
            }
            catch { }
        }

        private void BtnEditDetails_Click(object sender, RoutedEventArgs e)
        {
            var noteDlg = new NoteDialog(_record) { Owner = this };
            if (noteDlg.ShowDialog() == true)
            {
                WereDetailsModified = true;
                _onUpdateRequested?.Invoke(_record, noteDlg.CustomerText, noteDlg.TargetDeviceText, noteDlg.NoteText);
                RefreshUi();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
