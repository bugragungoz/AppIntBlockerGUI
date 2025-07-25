// <copyright file="App.xaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Management.Automation;
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using System.Windows;
    using AppIntBlockerGUI.Core;
    using AppIntBlockerGUI.Services;
    using AppIntBlockerGUI.ViewModels;
    using AppIntBlockerGUI.Views;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.ObjectPool;
    using Serilog;
    using Serilog.Events;

    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public static IServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // Register Services
                    services.AddSingleton<ILoggingService, LoggingService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    
                    // Add services that have dependencies
                    services.AddSingleton<ISettingsService, SettingsService>(provider => 
                        new SettingsService(
                            provider.GetRequiredService<ILoggingService>(),
                            provider.GetRequiredService<IDialogService>()));
                            
                    services.AddSingleton<ISystemRestoreService, SystemRestoreService>(provider =>
                        new SystemRestoreService(
                            provider.GetRequiredService<ILoggingService>()));

                    services.AddSingleton<INavigationService, NavigationService>();
                    
                    // Correctly register the PowerShell wrapper factory
                    services.AddSingleton<ObjectPool<PowerShell>>(provider =>
                    {
                        var poolProvider = new DefaultObjectPoolProvider();
                        return poolProvider.Create(new PowerShellPooledObjectPolicy());
                    });
                    services.AddTransient<Func<IPowerShellWrapper>>(provider => 
                        () => new PowerShellWrapper(provider.GetRequiredService<ObjectPool<PowerShell>>()));
                        
                    services.AddSingleton<IFirewallService, FirewallService>();
                    services.AddSingleton<INetworkMonitorService, NetworkMonitorService>();

                    // Register ViewModels
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<BlockApplicationViewModel>();
                    services.AddTransient<ManageRulesViewModel>();
                    services.AddTransient<RestorePointsViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<WindowsFirewallViewModel>();
                    services.AddSingleton<NetworkMonitorViewModel>();

                    // Register Views
                    services.AddTransient<MainWindow>();
                    services.AddTransient<BlockApplicationView>();
                    services.AddTransient<ManageRulesView>();
                    services.AddTransient<RestorePointsView>();
                    services.AddTransient<SettingsView>();
                    services.AddTransient<WindowsFirewallView>();
                    services.AddTransient<NetworkMonitorView>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    var logFilePath = Path.Combine(AppContext.BaseDirectory, "Logs", "AppIntBlockerGUI-.log");
                    var logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.File(
                            logFilePath,
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 7,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                        .CreateLogger();
                    logging.AddSerilog(logger);
                    Log.Logger = logger;
                })
                .Build();

            ServiceProvider = AppHost.Services;

            // Subscribe to unhandled exception events as early as possible so that we can surface
            // any unexpected failures to the user instead of silently terminating the elevated
            // instance and leaving the impression that the application did not start.
            this.DispatcherUnhandledException += this.Application_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            if (!IsRunAsAdministrator())
            {
                var dialogService = ServiceProvider!.GetRequiredService<IDialogService>();
                var restart = dialogService.ShowConfirmation("Administrator Privileges Required", "This application requires administrator privileges to function correctly. Would you like to restart as administrator?");
                if (restart)
                {
                    RestartAsAdministrator();
                }
                else
                {
                    dialogService.ShowWarning("Operation cancelled. The application will now exit.", "Operation Cancelled");
                }
                Current.Shutdown();
                return;
            }

            var mainWindow = ServiceProvider!.GetService<MainWindow>();
            mainWindow!.Show();
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogCritical(e.Exception, "An unhandled exception occurred");
            MessageBox.Show($"An unhandled exception occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Current.Shutdown();
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogCritical(exception, "An unhandled domain exception occurred");

            // Attempt to show a user-friendly message before we terminate.
            MessageBox.Show($"An unhandled exception occurred: {exception?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var exception = e.Exception;
            var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
            logger?.LogCritical(exception, "An unhandled task exception occurred");

            // Attempt to show a user-friendly message before we terminate.
            MessageBox.Show($"An unhandled task exception occurred: {exception?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }

        [SupportedOSPlatform("windows")]
        public static bool IsRunAsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        [SupportedOSPlatform("windows")]
        private void RestartAsAdministrator()
        {
            var exeName = Process.GetCurrentProcess().MainModule?.FileName;
            var startInfo = new ProcessStartInfo(exeName!)
            {
                Verb = "runas",
                UseShellExecute = true
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                var logger = ServiceProvider?.GetRequiredService<ILogger<App>>();
                logger?.LogError(ex, "Failed to restart application as administrator.");

                // Use the themed dialog service instead of the default message box so the look & feel
                // is consistent across all user interactions.
                var dialogService = ServiceProvider?.GetService<IDialogService>();

                // Native error code 1223 == ERROR_CANCELLED (operation cancelled by the user)
                if (ex is System.ComponentModel.Win32Exception win32Ex && win32Ex.NativeErrorCode == 1223)
                {
                    dialogService?.ShowWarning("Operation cancelled. The application will now exit.", "Operation Cancelled");
                }
                else
                {
                    dialogService?.ShowError("Failed to restart the application with administrator privileges.", "Error");
                }

                if (dialogService == null)
                {
                    // Fallback in the rare case DI container is not yet ready
                MessageBox.Show("Failed to restart the application with administrator privileges.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            Current.Shutdown();
        }
    }
}
