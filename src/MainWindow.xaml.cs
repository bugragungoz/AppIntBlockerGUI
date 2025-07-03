using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AppIntBlockerGUI.Views;
using AppIntBlockerGUI.ViewModels;
using MahApps.Metro.Controls;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace AppIntBlockerGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Asynchronously load the initial view to improve startup performance.
            // The main window shell appears instantly, and the content loads a moment later.
            if (DataContext is MainWindowViewModel viewModel)
            {
                await Task.Delay(50); // Small delay to ensure the window is fully rendered before loading content.
                viewModel.NavigateToBlockApplicationCommand.Execute(null);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Dispose the ViewModel if it implements IDisposable
            if (DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }
            base.OnClosing(e);
            Application.Current.Shutdown();
        }
    }
}