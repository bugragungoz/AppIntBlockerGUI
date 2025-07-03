using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using AppIntBlockerGUI.Services;
using AppIntBlockerGUI.ViewModels;
using System;
using System.Security.Principal;
using System.Diagnostics;
using AppIntBlockerGUI.Views;
using System.Threading.Tasks;

namespace AppIntBlockerGUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    
    public static IServiceProvider? ServiceProvider { get; private set; }

    public App()
    {
        // Global exception handlers
        this.DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show($"Unhandled Exception: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
                "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Fatal Exception: {ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}",
                "Fatal Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IFirewallService, FirewallService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<BlockApplicationViewModel>(); 
        services.AddTransient<ManageRulesViewModel>();
        services.AddTransient<RestorePointsViewModel>();
        services.AddTransient<WindowsFirewallViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Main Window
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // If already running as admin, proceed directly to loading.
        if (IsRunAsAdministrator())
        {
            var loadingWindow = new LoadingWindow();
            Application.Current.MainWindow = loadingWindow;
            loadingWindow.Show();

            loadingWindow.UpdateStatus("Initializing...");
            
            await Task.Run(async () =>
            {
                // Pre-load and initialize view models in the background
                var blockAppViewModel = _serviceProvider.GetRequiredService<BlockApplicationViewModel>();
                await blockAppViewModel.InitializeAsync();

                var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                await mainViewModel.LoadInitialDataAsync();
            });

            loadingWindow.UpdateStatus("Finalizing...");
            await Task.Delay(500); // Small delay for the message to be readable

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            Application.Current.MainWindow = mainWindow;
            
            loadingWindow.Close();
            mainWindow.Show();
            return;
        }

        // If not admin, require restart.
        var confirmationDialog = new CustomDialogWindow(
            "Administrator Privileges Required",
            "AppIntBlocker requires administrator privileges to manage Windows Firewall rules.\n\n" +
            "Would you like to restart the application as administrator?",
            "❓",
            showCancelButton: true);

        if (confirmationDialog.ShowDialog() == true)
        {
            if (!RestartAsAdministrator())
            {
                // Failure, UAC was likely denied. Show a message.
                var warningDialog = new CustomDialogWindow("Permission Denied", "Administrator privileges were not granted. The application cannot continue.", "⚠️");
                warningDialog.ShowDialog();
            }
        }
        else
        {
            // User canceled the restart request. Show a confirmation.
            var canceledDialog = new CustomDialogWindow("Operation Canceled", "The request to restart as administrator was canceled. The application will now close.", "ℹ️");
            canceledDialog.ShowDialog();
        }

        // After all dialogs are handled, explicitly shut down.
        Shutdown();
    }

    private bool IsRunAsAdministrator()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private bool RestartAsAdministrator()
    {
        string? exeName = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exeName))
        {
            exeName = Environment.ProcessPath;
        }

        if (string.IsNullOrEmpty(exeName))
        {
            // This is an internal error, so a simple MessageBox is fine.
            MessageBox.Show("Could not determine the application path to restart.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        var startInfo = new ProcessStartInfo(exeName)
        {
            Verb = "runas",
            UseShellExecute = true
        };
        
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // This exception is thrown when the user denies the UAC prompt.
            // We return false so the calling method can handle the UI message.
            return false;
        }
        catch (Exception)
        {
            // Handle other potential errors during process start
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
        base.OnExit(e);
    }

    // Global TextBox event handlers for focus behavior
    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            // Move focus to the parent to lose focus from the TextBox
            var parent = (UIElement)textBox.Parent;
            parent?.Focus();
            Keyboard.ClearFocus(); // Ensure focus is fully cleared
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // This can be used if additional logic is needed on lost focus
    }
}

