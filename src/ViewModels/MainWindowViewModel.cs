using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly INavigationService _navigationService;
        private readonly IFirewallService _firewallService;
        private readonly ILoggingService _loggingService;
        private readonly IDialogService _dialogService;
        private readonly DateTime _appStartTime;
        private readonly DispatcherTimer _uptimeTimer;

        [ObservableProperty]
        private object? _currentViewModel;
        
        [ObservableProperty]
        private string _uptime = "00:00:00";

        [ObservableProperty]
        private string _activeRulesCount = "0";

        [ObservableProperty]
        private string _lastUpdateTime = "Never";
        
        [ObservableProperty]
        private string _systemStatus = "Initializing...";

        public ICommand NavigateToBlockApplicationCommand { get; }
        public ICommand NavigateToManageRulesCommand { get; }
        public ICommand NavigateToRestorePointsCommand { get; }
        public ICommand NavigateToWindowsFirewallCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        public MainWindowViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            ILoggingService loggingService,
            IFirewallService firewallService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            _loggingService = loggingService;
            _firewallService = firewallService;

            NavigateToBlockApplicationCommand = new RelayCommand(() => _navigationService.NavigateTo(typeof(BlockApplicationViewModel)));
            NavigateToManageRulesCommand = new RelayCommand(() => _navigationService.NavigateTo(typeof(ManageRulesViewModel)));
            NavigateToWindowsFirewallCommand = new RelayCommand(() => _navigationService.NavigateTo(typeof(WindowsFirewallViewModel)));
            NavigateToRestorePointsCommand = new RelayCommand(() => _navigationService.NavigateTo(typeof(RestorePointsViewModel)));
            NavigateToSettingsCommand = new RelayCommand(() => _navigationService.NavigateTo(typeof(SettingsViewModel)));

            _navigationService.NavigationChanged += NavigationService_NavigationChanged;

            // Navigate to the default view on startup
            _navigationService.NavigateTo(typeof(BlockApplicationViewModel));

            _appStartTime = DateTime.Now;
            _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _uptimeTimer.Tick += (s, e) => {
                var elapsed = DateTime.Now - _appStartTime;
                Uptime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            };
            _uptimeTimer.Start();
        }

        private void NavigationService_NavigationChanged(ObservableObject newViewModel)
        {
            CurrentViewModel = newViewModel;
        }

        public async Task LoadInitialDataAsync()
        {
            await LoadStatisticsAsync();
        }
        
        private async Task LoadStatisticsAsync()
        {
            try
            {
                SystemStatus = "Loading...";
                var rules = await _firewallService.GetExistingRulesAsync(_loggingService);
                ActiveRulesCount = rules.Count.ToString();
                SystemStatus = rules.Any() ? "Protecting" : "Standby";
                LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to load statistics: {ex.Message}");
                SystemStatus = "Error";
            }
        }

        public void Dispose()
        {
            _uptimeTimer?.Stop();
        }
    }
} 