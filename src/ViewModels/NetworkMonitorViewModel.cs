namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.Windows.Threading;
    using AppIntBlockerGUI.Models;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using LiveChartsCore;
    using LiveChartsCore.Defaults; // Correct namespace for ObservablePoint
    using LiveChartsCore.SkiaSharpView;
    using LiveChartsCore.SkiaSharpView.Painting;
    using LiveChartsCore.SkiaSharpView.WPF; // Add this using directive
    using SkiaSharp;

    /// <summary>
    /// ViewModel that exposes live per-process network statistics to the view.
    /// </summary>
    public sealed partial class NetworkMonitorViewModel : ObservableObject, IDisposable, INotifyNavigated
    {
        private readonly INetworkMonitorService networkMonitorService;
        private readonly IFirewallService firewallService;
        private readonly ILoggingService loggingService;
        private bool monitoringStarted;
        private bool isBlockOperationRunning;
        private readonly ObservableCollection<ObservablePoint> uploadSeriesValues = new();
        private readonly ObservableCollection<ObservablePoint> downloadSeriesValues = new();
        private DateTime graphStartTime = DateTime.UtcNow;
        private readonly DispatcherTimer graphTimer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GraphTitle))]
        private bool isGraphShowingTotal = true;
        
        public string GraphTitle => this.IsGraphShowingTotal ? "Total Network Traffic" : $"Traffic for {this.SelectedProcess?.ProcessName}";

        public ObservableCollection<string> NetworkDevices { get; }
        
        [ObservableProperty]
        private string? selectedNetworkDevice;
        
        [ObservableProperty]
        private bool noDevicesFound;
        
        [ObservableProperty]
        private bool isChangingDevice;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkMonitorViewModel"/> class.
        /// </summary>
        public NetworkMonitorViewModel(
            INetworkMonitorService networkMonitorService,
            IFirewallService firewallService,
            ILoggingService loggingService)
        {
            this.networkMonitorService = networkMonitorService;
            this.firewallService = firewallService;
            this.loggingService = loggingService;

            this.ToggleBlockCommand = new AsyncRelayCommand(this.ToggleBlockAsync, () => this.SelectedProcess != null && !this.isBlockOperationRunning);
            this.graphTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            this.graphTimer.Tick += this.GraphTimer_Tick;

            this.ChartSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Name = "Download",
                    Values = this.downloadSeriesValues,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Fill = new LinearGradientPaint(SKColors.DodgerBlue.WithAlpha(90), SKColors.DodgerBlue.WithAlpha(10), new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 0,
                    LineSmoothness = 1
                },
                new LineSeries<ObservablePoint>
                {
                    Name = "Upload",
                    Values = this.uploadSeriesValues,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    Fill = new LinearGradientPaint(SKColors.OrangeRed.WithAlpha(90), SKColors.OrangeRed.WithAlpha(10), new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    GeometrySize = 0,
                    LineSmoothness = 1
                }
            };

            this.XAxes = new Axis[] { new Axis { Labeler = value => $"{value:0}s", Name = "Time (s)" } };
            this.YAxes = new Axis[] { new Axis { Name = "kbit/s" } };

            NetworkDevices = new ObservableCollection<string>(this.networkMonitorService.GetAvailableDevices());
            if (NetworkDevices.Any())
            {
                // Try to select the default network device first
                var defaultDevice = this.networkMonitorService.GetDefaultNetworkDevice();
                if (defaultDevice != null && NetworkDevices.Contains(defaultDevice))
                {
                    SelectedNetworkDevice = defaultDevice;
                    loggingService.LogInfo($"Auto-selected default network device: {defaultDevice}");
                }
                else
                {
                    SelectedNetworkDevice = NetworkDevices.First();
                    loggingService.LogInfo($"Auto-selected first available network device: {NetworkDevices.First()}");
                }
            }
        }

        /// <summary>
        /// Gets the live collection of network usage objects bound to the UI.
        /// </summary>
        public ObservableCollection<ProcessNetworkUsageModel> Usages => this.networkMonitorService.Usages;

        [ObservableProperty]
        private ProcessNetworkUsageModel? selectedProcess;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BlockButtonText))]
        private bool isSelectedProcessBlocked;

        public string BlockButtonText => this.IsSelectedProcessBlocked ? "Unblock Internet" : "Block Internet";

        public IAsyncRelayCommand ToggleBlockCommand { get; }

        public ISeries[] ChartSeries { get; }
        public Axis[] XAxes { get; }
        public Axis[] YAxes { get; }

        [ObservableProperty]
        private double totalUploadMbps;

        [ObservableProperty]
        private double totalDownloadMbps;

        [ObservableProperty]
        private double totalUploadedMb;

        [ObservableProperty]
        private double totalDownloadedMb;



        public void OnNavigatedTo()
        {
            loggingService.LogInfo("NetworkMonitorViewModel.OnNavigatedTo() called - starting network monitoring setup");

            if (monitoringStarted)
            {
                graphTimer.Start();
                return;
            }

            NetworkDevices.Clear();
            var devices = networkMonitorService.GetAvailableDevices();
            foreach (var device in devices)
            {
                NetworkDevices.Add(device);
            }

            NoDevicesFound = !NetworkDevices.Any();
            loggingService.LogInfo($"Network devices found: {NetworkDevices.Count}, NoDevicesFound: {NoDevicesFound}");

            if (!monitoringStarted && NetworkDevices.Any())
            {
                // Try to select the default network device first
                var defaultDevice = networkMonitorService.GetDefaultNetworkDevice();
                string deviceToSelect;
                
                if (defaultDevice != null && NetworkDevices.Contains(defaultDevice))
                {
                    deviceToSelect = defaultDevice;
                    loggingService.LogInfo($"Auto-selecting default network device: {deviceToSelect}");
                }
                else
                {
                    deviceToSelect = NetworkDevices.First();
                    loggingService.LogInfo($"Auto-selecting first available device: {deviceToSelect}");
                }
                
                // Directly set the property and trigger the monitoring async
                // to avoid any race conditions with the UI binding.
                SelectedNetworkDevice = deviceToSelect;
                _ = ChangeDeviceAsync(deviceToSelect);
            }
        }

        public void OnNavigatedFrom()
        {
            // Only stop the UI timer, not the background monitoring service.
            graphTimer.Stop();
        }
        
        partial void OnSelectedNetworkDeviceChanged(string? value)
        {
            loggingService.LogInfo($"OnSelectedNetworkDeviceChanged called with value: {value}, IsChangingDevice: {IsChangingDevice}");
            
            if (value != null && !IsChangingDevice)
            {
                loggingService.LogInfo($"Starting device change to: {value}");
                _ = ChangeDeviceAsync(value);
            }
        }

        private async Task ChangeDeviceAsync(string deviceName)
        {
            loggingService.LogInfo($"ChangeDeviceAsync started for device: {deviceName}");
            IsChangingDevice = true;
            try
            {
                loggingService.LogInfo($"Device selection changed. Monitoring '{deviceName}'.");
                await networkMonitorService.StartMonitoringAsync(deviceName);
                monitoringStarted = true;
                loggingService.LogInfo("Monitoring started successfully, starting graph timer");
                if (!graphTimer.IsEnabled)
                {
                    graphTimer.Start();
                    loggingService.LogInfo("Graph timer started");
                }
            }
            catch (Exception ex)
            {
                loggingService.LogError($"Error in ChangeDeviceAsync: {ex.Message}", ex);
            }
            finally
            {
                IsChangingDevice = false;
                loggingService.LogInfo("ChangeDeviceAsync completed");
            }
        }
        
        public void Dispose()
        {
            // Stop everything on dispose.
            if (monitoringStarted)
            {
                graphTimer.Stop();
                networkMonitorService.StopMonitoringAsync(CancellationToken.None).ConfigureAwait(false);
                monitoringStarted = false;
            }
        }

        private void GraphTimer_Tick(object? sender, EventArgs e)
        {
            this.TotalUploadMbps = this.networkMonitorService.Usages.Sum(u => u.UploadKbps);
            this.TotalDownloadMbps = this.networkMonitorService.Usages.Sum(u => u.DownloadKbps);
            this.TotalUploadedMb = this.networkMonitorService.Usages.Sum(u => u.TotalSentMB);
            this.TotalDownloadedMb = this.networkMonitorService.Usages.Sum(u => u.TotalReceivedMB);

            var elapsed = (DateTime.UtcNow - this.graphStartTime).TotalSeconds;
            double upload = 0;
            double download = 0;

            if (this.IsGraphShowingTotal)
            {
                upload = this.TotalUploadMbps;
                download = this.TotalDownloadMbps;
            }
            else if (this.SelectedProcess != null)
            {
                upload = this.SelectedProcess.UploadKbps;
                download = this.SelectedProcess.DownloadKbps;
            }

            this.uploadSeriesValues.Add(new ObservablePoint(elapsed, upload));
            this.downloadSeriesValues.Add(new ObservablePoint(elapsed, download));

            if (this.uploadSeriesValues.Count > 60)
            {
                this.uploadSeriesValues.RemoveAt(0);
                this.downloadSeriesValues.RemoveAt(0);
            }

            // Convert threshold to Mbps for comparison (AlertThresholdKbps was in old kbps)
            const double AlertThresholdMbps = 5.0; // 5 Mbps
            if (this.TotalDownloadMbps > AlertThresholdMbps || this.TotalUploadMbps > AlertThresholdMbps)
            {
                this.loggingService.LogWarning($"Throughput exceeded {AlertThresholdMbps} Mbps");
            }
        }

        partial void OnSelectedProcessChanged(ProcessNetworkUsageModel? value)
        {
            this.IsGraphShowingTotal = value == null;
            this.uploadSeriesValues.Clear();
            this.downloadSeriesValues.Clear();
            this.graphStartTime = DateTime.UtcNow;
            
            // This ensures the button's enabled/disabled state updates immediately
            this.ToggleBlockCommand.NotifyCanExecuteChanged();
            
            // Update the blocked status asynchronously
            _ = this.UpdateBlockedStatusAsync();
        }

        private async Task UpdateBlockedStatusAsync()
        {
            if (this.SelectedProcess == null || string.IsNullOrEmpty(this.SelectedProcess.Path))
            {
                this.IsSelectedProcessBlocked = false;
                return;
            }

            try
            {
                var directoryName = Path.GetDirectoryName(this.SelectedProcess.Path);
                if (string.IsNullOrEmpty(directoryName))
                {
                    this.IsSelectedProcessBlocked = false;
                    return;
                }

                var appName = Path.GetFileName(directoryName);
                if (string.IsNullOrEmpty(appName))
                {
                    this.IsSelectedProcessBlocked = false;
                    return;
                }

                var rules = await this.firewallService.GetExistingRulesAsync();
                this.IsSelectedProcessBlocked = rules.Any(r => r.Contains(appName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Failed to determine block status for {this.SelectedProcess.ProcessName}: {ex.Message}");
                this.IsSelectedProcessBlocked = false;
            }
        }

        private async Task ToggleBlockAsync()
        {
            if (this.SelectedProcess == null || string.IsNullOrEmpty(this.SelectedProcess.Path)) return;

            this.isBlockOperationRunning = true;
            this.ToggleBlockCommand.NotifyCanExecuteChanged();

            try
            {
                var dirPath = Path.GetDirectoryName(this.SelectedProcess.Path)!;
                var appName = Path.GetFileName(dirPath);

                if (this.IsSelectedProcessBlocked)
                {
                    await this.firewallService.RemoveExistingRules(appName, this.loggingService);
                }
                else
                {
                    await this.firewallService.BlockApplicationFiles(
                        dirPath, true, false, false,
                        new List<string>(), new List<string>(), this.loggingService, CancellationToken.None);
                }
            }
            finally
            {
                this.isBlockOperationRunning = false;
                await this.UpdateBlockedStatusAsync();
                this.ToggleBlockCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void BlockSelectedProcess()
        {
            // This command is not fully implemented in the original file,
            // so it's left as a placeholder.
            // The original ToggleBlockCommand handles the actual blocking/unblocking.
        }
    }
}