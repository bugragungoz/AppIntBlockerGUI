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
using System.Windows.Threading;

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
        this.DispatcherUnhandledException += Application_DispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Fatal Exception: {ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}",
                "Fatal Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        ServiceProvider = _serviceProvider; // Set static reference
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Use fire-and-forget with proper exception handling
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeApplicationAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log the exception and show user-friendly message
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Application initialization failed: {ex.Message}", 
                        "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown(1);
                });
            }
        });
    }

    private async Task InitializeApplicationAsync()
    {
        // If already running as admin, proceed directly to loading.
        if (IsRunAsAdministrator())
        {
            LoadingWindow? loadingWindow = null;
            MainWindow? mainWindow = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                loadingWindow = new LoadingWindow();
                Application.Current.MainWindow = loadingWindow;
                loadingWindow.Show();
                loadingWindow.UpdateStatus("Initializing...");
            });

            // Background initialization
            var blockAppViewModel = _serviceProvider.GetRequiredService<BlockApplicationViewModel>();
            await blockAppViewModel.InitializeAsync().ConfigureAwait(false);

            var mainViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            await mainViewModel.LoadInitialDataAsync().ConfigureAwait(false);

            // Switch to main window on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                loadingWindow?.UpdateStatus("Finalizing...");
                mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = mainWindow;
                loadingWindow?.Close();
                mainWindow.Show();
            });

            await Task.Delay(500).ConfigureAwait(false); // Small delay for the message to be readable
            return;
        }

        // If not admin, require restart - handle on UI thread
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
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
        });
    }

    public static bool IsRunAsAdministrator()
    {
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var principal = new WindowsPrincipal(identity);
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
        // CRITICAL FIX: Dispose static ServiceProvider
        if (ServiceProvider is IDisposable disposableStatic)
            disposableStatic.Dispose();
            
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

    /// <summary>
    /// Sanitizes error messages to prevent sensitive information disclosure.
    /// AI-generated code: Enhanced error handling for security.
    /// </summary>
    private static string SanitizeErrorMessage(Exception exception)
    {
        // Don't expose detailed error information that could help attackers
        var exceptionType = exception.GetType().Name;
        
        // Map specific exceptions to safe, user-friendly messages
        return exceptionType switch
        {
            "UnauthorizedAccessException" => "Access was denied. Please check your permissions.",
            "FileNotFoundException" => "A required file could not be found.",
            "DirectoryNotFoundException" => "A required directory could not be found.",
            "SecurityException" => "A security error occurred. Please contact support.",
            "InvalidOperationException" => "An invalid operation was attempted.",
            "ArgumentException" => "Invalid input was provided.",
            "Win32Exception" => "A system operation failed. Please try again.",
            "SqlException" => "A database error occurred. Please try again later.",
            "HttpRequestException" => "A network error occurred. Please check your connection.",
            "TimeoutException" => "The operation timed out. Please try again.",
            _ => "An unexpected error occurred. Please restart the application and try again."
        };
    }

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var exceptionType = e.Exception.GetType().Name;
        
        // Sanitize the error message to prevent information disclosure
        var userFriendlyMessage = SanitizeErrorMessage(e.Exception);

        // Log the full exception details for debugging purposes (but not to user)
        // In production, this should go to a secure logging system
        Debug.WriteLine($"[FATAL] Unhandled Exception: {e.Exception}");
        
        // Only show sanitized, user-friendly message to the user
        MessageBox.Show(
            $"{userFriendlyMessage}\n\nError ID: {Guid.NewGuid():N}\n\nIf this problem persists, please contact support with the Error ID.",
            "Application Error", 
            MessageBoxButton.OK, 
            MessageBoxImage.Error);

        e.Handled = true;
        Current.Shutdown();
    }
}

