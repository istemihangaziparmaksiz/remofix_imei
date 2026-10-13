using System.Windows;
using ImeiStudio.Models;

namespace ImeiStudio
{
    public partial class ConfirmDeleteDialog : Window
    {
        public ConfirmDeleteDialog(ImeiRecord record)
        {
            InitializeComponent();
            if (record != null)
            {
                txtImei.Text = record.Imei;
                txtModel.Text = string.IsNullOrEmpty(record.BrandModel) || record.BrandModel == "-"
                    ? "Model: Belirtilmemiş"
                    : $"Model: {record.BrandModel}";
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
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
