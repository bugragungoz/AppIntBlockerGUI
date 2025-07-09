namespace AppIntBlockerGUI.Views
{
    using System.Diagnostics;
    using System.Windows.Controls;
    using System.Windows.Navigation;

    /// <summary>
    /// Interaction logic for NetworkMonitorView.xaml
    /// </summary>
    public partial class NetworkMonitorView : UserControl
    {
        public NetworkMonitorView()
        {
            this.InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}