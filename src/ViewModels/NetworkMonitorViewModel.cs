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
        [NotifyPropertyChangedFor(nameof(TotalSentRateFormatted))]
        private double totalUploadMbps;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalReceivedRateFormatted))]
        private double totalDownloadMbps;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSentFormatted))]
        private double totalUploadedMb;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalReceivedFormatted))]
        private double totalDownloadedMb;

        /// <summary>
        /// Gets formatted total sent rate with appropriate units.
        /// </summary>
        public string TotalSentRateFormatted 
        {
            get
            {
                var kbps = TotalUploadMbps * 1024; // Convert from Mbps to kbps
                if (kbps < 1000)
                    return $"{kbps:F0} kbit/s";
                else
                    return $"{TotalUploadMbps:F2} Mbit/s";
            }
        }

        /// <summary>
        /// Gets formatted total received rate with appropriate units.
        /// </summary>
        public string TotalReceivedRateFormatted 
        {
            get
            {
                var kbps = TotalDownloadMbps * 1024; // Convert from Mbps to kbps
                if (kbps < 1000)
                    return $"{kbps:F0} kbit/s";
                else
                    return $"{TotalDownloadMbps:F2} Mbit/s";
            }
        }

        /// <summary>
        /// Gets formatted total sent data with appropriate units.
        /// </summary>
        public string TotalSentFormatted 
        {
            get
            {
                if (TotalUploadedMb < 1)
                    return $"{TotalUploadedMb * 1024:F0} KB";
                else if (TotalUploadedMb < 1024)
                    return $"{TotalUploadedMb:F1} MB";
                else
                    return $"{TotalUploadedMb / 1024:F2} GB";
            }
        }

        /// <summary>
        /// Gets formatted total received data with appropriate units.
        /// </summary>
        public string TotalReceivedFormatted 
        {
            get
            {
                if (TotalDownloadedMb < 1)
                    return $"{TotalDownloadedMb * 1024:F0} KB";
                else if (TotalDownloadedMb < 1024)
                    return $"{TotalDownloadedMb:F1} MB";
                else
                    return $"{TotalDownloadedMb / 1024:F2} GB";
            }
        }



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

            // Trigger UI sorting update every few seconds to keep most active processes at top
            SortProcessesByActivity();
        }

        private void SortProcessesByActivity()
        {
            try
            {
                // Create a sorted list of processes
                var sortedProcesses = this.networkMonitorService.Usages
                    .OrderByDescending(p => p.UploadKbps + p.DownloadKbps) // Current activity first
                    .ThenByDescending(p => p.TotalSentMB + p.TotalReceivedMB) // Total usage second
                    .ToList();

                // Check if reordering is needed
                bool needsReorder = false;
                for (int i = 0; i < Math.Min(sortedProcesses.Count, this.networkMonitorService.Usages.Count); i++)
                {
                    if (!ReferenceEquals(sortedProcesses[i], this.networkMonitorService.Usages[i]))
                    {
                        needsReorder = true;
                        break;
                    }
                }

                // Only update if order has changed to avoid unnecessary UI updates
                if (needsReorder)
                {
                    var originalUsages = this.networkMonitorService.Usages;
                    originalUsages.Clear();
                    foreach (var process in sortedProcesses)
                    {
                        originalUsages.Add(process);
                    }
                    this.loggingService.LogDebug($"Reordered {sortedProcesses.Count} processes by network activity");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error sorting processes by activity", ex);
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

            // System protection check
            if (!this.IsSelectedProcessBlocked && IsSystemCriticalProcess(this.SelectedProcess))
            {
                var processName = this.SelectedProcess.ProcessName;
                
                // Critical system processes that should never be blocked
                if (IsCriticalSystemProcess(processName))
                {
                    loggingService.LogWarning($"Attempted to block critical system process: {processName}");
                    System.Windows.MessageBox.Show(
                        $"Cannot block '{processName}' as it is a critical system process required for Windows operation.\n\nBlocking this process could cause system instability.",
                        "System Protection - Critical Process",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }
                
                // Important system processes that require confirmation
                var result = System.Windows.MessageBox.Show(
                    $"Warning: '{processName}' appears to be a system process.\n\n" +
                    $"Blocking this process may affect system functionality or Windows services.\n\n" +
                    $"Are you sure you want to proceed?",
                    "System Protection - Confirm Action",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                
                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    loggingService.LogInfo($"User cancelled blocking system process: {processName}");
                    return;
                }
                
                loggingService.LogWarning($"User confirmed blocking system process: {processName}");
            }

            this.isBlockOperationRunning = true;
            this.ToggleBlockCommand.NotifyCanExecuteChanged();

            try
            {
                var dirPath = Path.GetDirectoryName(this.SelectedProcess.Path)!;
                var appName = Path.GetFileName(dirPath);

                if (this.IsSelectedProcessBlocked)
                {
                    await this.firewallService.RemoveExistingRules(appName, this.loggingService);
                    loggingService.LogInfo($"Unblocked application: {this.SelectedProcess.ProcessName}");
                }
                else
                {
                    await this.firewallService.BlockApplicationFiles(
                        dirPath, true, false, false,
                        new List<string>(), new List<string>(), this.loggingService, CancellationToken.None);
                    loggingService.LogInfo($"Blocked application: {this.SelectedProcess.ProcessName}");
                }
            }
            finally
            {
                this.isBlockOperationRunning = false;
                await this.UpdateBlockedStatusAsync();
                this.ToggleBlockCommand.NotifyCanExecuteChanged();
            }
        }

        private bool IsSystemCriticalProcess(ProcessNetworkUsageModel process)
        {
            var processName = process.ProcessName.ToLower();
            
            // Check if it's a system process
            return processName.Contains("system") ||
                   processName.Contains("svc") ||
                   processName.Contains("host") ||
                   processName.Contains("service") ||
                   processName.Contains("dwm") ||
                   processName.Contains("csrss") ||
                   processName.Contains("wininit") ||
                   processName.Contains("winlogon") ||
                   processName.Contains("lsass") ||
                   processName.Contains("explorer") ||
                   processName.StartsWith("microsoft") ||
                   processName.StartsWith("windows") ||
                   process.Path.ToLower().Contains("windows\\system32") ||
                   process.Path.ToLower().Contains("windows\\syswow64");
        }

        private bool IsCriticalSystemProcess(string processName)
        {
            var criticalProcesses = new[]
            {
                "system", "csrss", "wininit", "winlogon", "lsass", "services",
                "dwm", "explorer", "conhost", "fontdrvhost", "wmiprvse",
                "runtimebroker", "sihost", "taskhostw", "ctfmon", "spoolsv",
                "audiodg", "powershell", "cmd", "mmc", "dllhost"
            };
            
            return criticalProcesses.Any(critical => 
                processName.ToLower().Contains(critical));
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