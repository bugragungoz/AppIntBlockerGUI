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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Use fire-and-forget with exception handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadInitialViewAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Failed to load initial view: {ex.Message}", 
                            "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            });
        }

        private async Task LoadInitialViewAsync()
        {
            await Task.Delay(50).ConfigureAwait(false); // Small delay

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.NavigateToBlockApplicationCommand.Execute(null);
                }
            });
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