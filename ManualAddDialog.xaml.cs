using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImeiStudio.Models;
using ImeiStudio.Services;

namespace ImeiStudio
{
    public partial class ManualAddDialog : Window
    {
        public ImeiRecord? CreatedRecord { get; private set; }

        public ManualAddDialog()
        {
            InitializeComponent();
            txtImeiInput.Focus();
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void TxtImeiInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtLuhnPreview == null) return;
            string digits = Regex.Replace(txtImeiInput.Text ?? "", @"\D", "");
            if (digits.Length == 14)
            {
                int cd = LuhnHelper.CalculateCheckDigit(digits);
                txtLuhnPreview.Text = $"Tam IMEI: {digits}{cd} (15. Hane: {cd})";
            }
            else if (digits.Length == 15)
            {
                txtLuhnPreview.Text = $"Tam IMEI: {digits} (15 Hane)";
            }
            else
            {
                txtLuhnPreview.Text = $"Girilen: {digits.Length} hane (14 veya 15 hane girin)";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string digits = Regex.Replace(txtImeiInput.Text ?? "", @"\D", "");
            if (digits.Length == 14)
            {
                int cd = LuhnHelper.CalculateCheckDigit(digits);
                if (cd >= 0) digits += cd.ToString();
            }

            if (digits.Length != 15)
            {
                txtError.Text = "Lütfen geçerli bir 14 veya 15 haneli IMEI numarası giriniz.";
                txtError.Visibility = Visibility.Visible;
                return;
            }

            string model = string.IsNullOrWhiteSpace(txtBrandModel.Text) ? "Manuel Kayıt" : txtBrandModel.Text.Trim();
            string note = (txtNote.Text ?? "").Trim();

            CreatedRecord = new ImeiRecord
            {
                Imei = digits,
                BrandModel = model,
                Status = "KAYITLI (Manuel Eklendi)",
                Category = "KAYITLI",
                Source = "Manuel Giriş",
                Note = note,
                IsUsed = false,
                QueryDate = DateTime.Now
            };

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
