// <copyright file="MainWindowViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.Windows.Threading;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.DependencyInjection;

    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly INavigationService navigationService;
        private readonly IFirewallService firewallService;
        private readonly ILoggingService loggingService;
        private readonly IDialogService dialogService;
        private readonly DateTime appStartTime;
        private readonly DispatcherTimer uptimeTimer;

        [ObservableProperty]
        private object? currentViewModel;

        [ObservableProperty]
        private string uptime = "00:00:00";

        [ObservableProperty]
        private string activeRulesCount = "0";

        [ObservableProperty]
        private string lastUpdateTime = "Never";

        [ObservableProperty]
        private string systemStatus = "Initializing...";

        public ICommand NavigateToBlockApplicationCommand { get; }

        public ICommand NavigateToManageRulesCommand { get; }

        public ICommand NavigateToRestorePointsCommand { get; }

        public ICommand NavigateToWindowsFirewallCommand { get; }

        public ICommand NavigateToSettingsCommand { get; }

        public ICommand NavigateToNetworkMonitorCommand { get; }

        public MainWindowViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            ILoggingService loggingService,
            IFirewallService firewallService)
        {
            this.navigationService = navigationService;
            this.dialogService = dialogService;
            this.loggingService = loggingService;
            this.firewallService = firewallService;

            this.NavigateToBlockApplicationCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(BlockApplicationViewModel)));
            this.NavigateToManageRulesCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(ManageRulesViewModel)));
            this.NavigateToWindowsFirewallCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(WindowsFirewallViewModel)));
            this.NavigateToRestorePointsCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(RestorePointsViewModel)));
            this.NavigateToSettingsCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(SettingsViewModel)));
            this.NavigateToNetworkMonitorCommand = new RelayCommand(() => this.navigationService.NavigateTo(typeof(NetworkMonitorViewModel)));

            this.navigationService.NavigationChanged += this.NavigationService_NavigationChanged;

            // Navigate to the default view on startup
            this.navigationService.NavigateTo(typeof(BlockApplicationViewModel));

            this.appStartTime = DateTime.Now;
            this.uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            this.uptimeTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - this.appStartTime;
                this.Uptime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            };
            this.uptimeTimer.Start();
        }

        private void NavigationService_NavigationChanged(ObservableObject newViewModel)
        {
            this.CurrentViewModel = newViewModel;
        }

        public async Task LoadInitialDataAsync()
        {
            await this.LoadStatisticsAsync();
        }

        private async Task LoadStatisticsAsync()
        {
            try
            {
                this.SystemStatus = "Loading...";
                var rules = await this.firewallService.GetExistingRulesAsync(this.loggingService);
                this.ActiveRulesCount = rules.Count.ToString();
                this.SystemStatus = rules.Any() ? "Protecting" : "Standby";
                this.LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Failed to load statistics: {ex.Message}");
                this.SystemStatus = "Error";
            }
        }

        public void Dispose()
        {
            this.uptimeTimer?.Stop();
        }
    }
}
