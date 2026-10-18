using System.Windows;

namespace ImeiStudio
{
    public class VaultCheckSummary
    {
        public int TotalChecked { get; set; }
        public int RegisteredCount { get; set; }
        public int ClonedCount { get; set; }
        public int UnregisteredCount { get; set; }
        public int ClosedCount { get; set; }
        public int ErrorCount { get; set; }
    }

    public partial class VaultCheckResultDialog : Window
    {
        /// <summary>
        /// True ise kullanıcı "Kayıtlı IMEI Numaraları Kalsın" seçeneğini seçmiştir.
        /// False ise "Olduğu Gibi Bırak" seçeneğini seçmiştir.
        /// </summary>
        public bool KeepOnlyRegistered { get; private set; } = false;

        public VaultCheckResultDialog(VaultCheckSummary summary)
        {
            InitializeComponent();

            txtTotalCount.Text = $"{summary.TotalChecked} Adet";
            txtRegisteredCount.Text = $"{summary.RegisteredCount} Adet";
            txtClonedCount.Text = $"{summary.ClonedCount} Adet";
            txtUnregisteredCount.Text = $"{summary.UnregisteredCount} Adet";
            txtClosedCount.Text = $"{summary.ClosedCount} Adet";

            if (summary.ErrorCount > 0)
            {
                txtErrorCount.Text = $"{summary.ErrorCount} Adet";
                rowErrorCount.Visibility = Visibility.Visible;
            }
        }

        private void BtnLeaveAsIs_Click(object sender, RoutedEventArgs e)
        {
            KeepOnlyRegistered = false;
            DialogResult = true;
            Close();
        }

        private void BtnKeepOnlyRegistered_Click(object sender, RoutedEventArgs e)
        {
            KeepOnlyRegistered = true;
            DialogResult = true;
            Close();
        }
    }
}
