using System;
using System.Windows;
using ImeiStudio.Models;

namespace ImeiStudio
{
    public partial class MarkUsedDialog : Window
    {
        private readonly string _copyPayload;

        public string CustomerText { get; private set; } = string.Empty;
        public string TargetDeviceText { get; private set; } = string.Empty;
        public string NoteText { get; private set; } = string.Empty;

        public MarkUsedDialog(ImeiRecord record)
        {
            InitializeComponent();
            _copyPayload = record?.Imei ?? string.Empty;

            if (record != null)
            {
                txtImei.Text = record.Imei;
                txtModel.Text = string.IsNullOrEmpty(record.BrandModel) || record.BrandModel == "-"
                    ? "Kayıtlı Model: Belirtilmemiş"
                    : $"Kayıtlı Model: {record.BrandModel}";
                txtCustomer.Text = record.CustomerInfo ?? string.Empty;
                txtTargetDevice.Text = record.TargetDevice ?? string.Empty;
                txtNote.Text = record.Note ?? string.Empty;
            }

            txtCustomer.Focus();
        }

        public MarkUsedDialog(ImeiRecord sim1, ImeiRecord sim2)
        {
            InitializeComponent();
            _copyPayload = $"{sim1.Imei}{Environment.NewLine}{sim2.Imei}";

            Title = "Çift SIM (SIM 1 + SIM 2) Eşleştir ve Kullanıldı İşaretle";
            txtDialogTitle.Text = "Çift SIM (SIM 1 + SIM 2) Olarak İşaretle";
            txtDialogSubtitle.Text = "Seçilen 2 IMEI numarası eşleştirilerek aynı müşteri ve cihaz kaydıyla kullanılanlara aktarılacaktır.";
            btnQuickCopy.Content = "SIM 1 + SIM 2 Kopyala";
            btnConfirm.Content = "Çift SIM Kullanıldı Kaydet";

            badgeSim1.Visibility = Visibility.Visible;
            panelSim2.Visibility = Visibility.Visible;

            txtImei.Text = sim1.Imei;
            txtModel.Text = string.IsNullOrEmpty(sim1.BrandModel) || sim1.BrandModel == "-"
                ? "Kayıtlı Model: Belirtilmemiş"
                : $"Kayıtlı Model: {sim1.BrandModel}";

            txtImei2.Text = sim2.Imei;
            txtModel2.Text = string.IsNullOrEmpty(sim2.BrandModel) || sim2.BrandModel == "-"
                ? "Kayıtlı Model: Belirtilmemiş"
                : $"Kayıtlı Model: {sim2.BrandModel}";

            txtCustomer.Focus();
        }

        private void BtnQuickCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_copyPayload))
            {
                Clipboard.SetText(_copyPayload);
                btnQuickCopy.Content = "✓ Kopyalandı";
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            CustomerText = txtCustomer.Text.Trim();
            TargetDeviceText = txtTargetDevice.Text.Trim();
            NoteText = txtNote.Text.Trim();
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
