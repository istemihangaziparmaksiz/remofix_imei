using System.Windows;

namespace ImeiStudio
{
    public partial class NoteDialog : Window
    {
        public string NoteText { get; private set; } = string.Empty;

        public NoteDialog(string initialNote)
        {
            InitializeComponent();
            txtNote.Text = initialNote ?? string.Empty;
            txtNote.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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
