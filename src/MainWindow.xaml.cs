// <copyright file="MainWindow.xaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI
{
    using System.Diagnostics;
    using System.Reflection;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using AppIntBlockerGUI.ViewModels;
    using AppIntBlockerGUI.Views;
    using MahApps.Metro.Controls;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            this.InitializeComponent();
            this.DataContext = viewModel;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Use fire-and-forget with exception handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await this.LoadInitialViewAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"Failed to load initial view: {ex.Message}",
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
                if (this.DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.NavigateToBlockApplicationCommand.Execute(null);
                }
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Dispose the ViewModel if it implements IDisposable
            if (this.DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }

            base.OnClosing(e);
            Application.Current.Shutdown();
        }
    }
}
