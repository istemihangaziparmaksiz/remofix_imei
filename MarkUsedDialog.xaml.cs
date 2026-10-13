using System.Windows;
using ImeiStudio.Models;

namespace ImeiStudio
{
    public partial class MarkUsedDialog : Window
    {
        public string NoteText { get; private set; } = string.Empty;

        public MarkUsedDialog(ImeiRecord record)
        {
            InitializeComponent();
            if (record != null)
            {
                txtImei.Text = record.Imei;
                txtModel.Text = string.IsNullOrEmpty(record.BrandModel) || record.BrandModel == "-" 
                    ? "Model: Belirtilmemiş" 
                    : $"Model: {record.BrandModel}";
                txtNote.Text = record.Note ?? string.Empty;
            }
            txtNote.Focus();
            if (!string.IsNullOrEmpty(txtNote.Text))
            {
                txtNote.CaretIndex = txtNote.Text.Length;
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
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
