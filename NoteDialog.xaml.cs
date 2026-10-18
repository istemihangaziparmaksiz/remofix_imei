using System.Windows;
using ImeiStudio.Models;

namespace ImeiStudio
{
    public partial class NoteDialog : Window
    {
        public string CustomerText { get; private set; } = string.Empty;
        public string TargetDeviceText { get; private set; } = string.Empty;
        public string NoteText { get; private set; } = string.Empty;

        public NoteDialog(ImeiRecord record)
        {
            InitializeComponent();
            if (record != null)
            {
                txtCustomer.Text = record.CustomerInfo ?? string.Empty;
                txtTargetDevice.Text = record.TargetDevice ?? string.Empty;
                txtNote.Text = record.Note ?? string.Empty;
            }
            txtCustomer.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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
