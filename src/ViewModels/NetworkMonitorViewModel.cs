namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.Input;
    using System.Linq;
    using System.IO;
    using System.Windows.Threading;
    using LiveChartsCore;
    using LiveChartsCore.SkiaSharpView;
    using LiveChartsCore.SkiaSharpView.Painting;
    using SkiaSharp;

    /// <summary>
    /// ViewModel that exposes live per-process network statistics to the view.
    /// </summary>
    public sealed partial class NetworkMonitorViewModel : ObservableObject, IDisposable
    {
        private readonly INetworkMonitorService networkMonitorService;
        private readonly IFirewallService firewallService;
        private readonly ILoggingService loggingService;
        private bool monitoringStarted;
        private bool isBlockOperationRunning;
        private readonly ObservableCollection<LiveChartsCore.Measure.ObservablePoint> uploadSeriesValues = new();
        private readonly ObservableCollection<LiveChartsCore.Measure.ObservablePoint> downloadSeriesValues = new();
        private DateTime graphStartTime = DateTime.UtcNow;
        private readonly DispatcherTimer graphTimer;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkMonitorViewModel"/> class.
        /// </summary>
        public NetworkMonitorViewModel(INetworkMonitorService networkMonitorService,
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
                new LineSeries<LiveChartsCore.Measure.ObservablePoint>
                {
                    Values = this.uploadSeriesValues,
                    Name = "Upload",
                    Stroke = new SolidColorPaint(SKColors.SkyBlue, 2),
                    Fill = null
                },
                new LineSeries<LiveChartsCore.Measure.ObservablePoint>
                {
                    Values = this.downloadSeriesValues,
                    Name = "Download",
                    Stroke = new SolidColorPaint(SKColors.LimeGreen, 2),
                    Fill = null
                }
            };

            this.XAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value => $"{value:0}s",
                    Name = "Time (s)"
                }
            };

            this.YAxes = new Axis[]
            {
                new Axis
                {
                    Name = "kbit/s"
                }
            };
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
        private double totalUploadKbps;

        [ObservableProperty]
        private double totalDownloadKbps;

        [ObservableProperty]
        private double totalUploadedMb;

        [ObservableProperty]
        private double totalDownloadedMb;

        private const double AlertThresholdKbps = 5000; // 5 Mbps

        /// <summary>
        /// Called by the <see cref="Services.NavigationService"/> when navigation switches to this view.
        /// We start monitoring here so that resources are used only when the page is visible.
        /// </summary>
        public Task InitializeAsync()
        {
            if (!this.monitoringStarted)
            {
                this.networkMonitorService.StartMonitoring();
                this.monitoringStarted = true;
                this.graphStartTime = DateTime.UtcNow;
                this.uploadSeriesValues.Clear();
                this.downloadSeriesValues.Clear();
                this.graphTimer.Start();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = this.networkMonitorService.StopMonitoringAsync();
            this.graphTimer.Stop();
        }

        private void GraphTimer_Tick(object? sender, EventArgs e)
        {
            if (this.SelectedProcess == null)
            {
                return;
            }

            var elapsed = (DateTime.UtcNow - this.graphStartTime).TotalSeconds;
            this.uploadSeriesValues.Add(new LiveChartsCore.Measure.ObservablePoint(elapsed, this.SelectedProcess.UploadKbps));
            this.downloadSeriesValues.Add(new LiveChartsCore.Measure.ObservablePoint(elapsed, this.SelectedProcess.DownloadKbps));

            // Update aggregates
            this.TotalUploadKbps = this.networkMonitorService.Usages.Sum(u => u.UploadKbps);
            this.TotalDownloadKbps = this.networkMonitorService.Usages.Sum(u => u.DownloadKbps);
            this.TotalUploadedMb = this.networkMonitorService.Usages.Sum(u => u.TotalSentMB);
            this.TotalDownloadedMb = this.networkMonitorService.Usages.Sum(u => u.TotalReceivedMB);

            if (this.TotalDownloadKbps > AlertThresholdKbps || this.TotalUploadKbps > AlertThresholdKbps)
            {
                this.loggingService.LogWarning($"Throughput exceeded {AlertThresholdKbps / 1000} Mbps");
            }

            // Keep only last 60 points
            if (this.uploadSeriesValues.Count > 60)
            {
                this.uploadSeriesValues.RemoveAt(0);
                this.downloadSeriesValues.RemoveAt(0);
            }
        }

        partial void OnSelectedProcessChanged(ProcessNetworkUsageModel? value)
        {
            this.uploadSeriesValues.Clear();
            this.downloadSeriesValues.Clear();
            this.graphStartTime = DateTime.UtcNow;
            _ = this.UpdateBlockedStatusAsync();
            this.ToggleBlockCommand.NotifyCanExecuteChanged();
        }

        private async Task UpdateBlockedStatusAsync()
        {
            if (this.SelectedProcess == null)
            {
                this.IsSelectedProcessBlocked = false;
                return;
            }

            try
            {
                var rules = await this.firewallService.GetExistingRulesAsync(this.loggingService);
                var appName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(this.SelectedProcess.Path));
                var blocked = rules.Any(r => r.Contains(appName, StringComparison.OrdinalIgnoreCase));
                this.IsSelectedProcessBlocked = blocked;
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Failed to determine block status: {ex.Message}");
            }
        }

        private async Task ToggleBlockAsync()
        {
            if (this.SelectedProcess == null || string.IsNullOrEmpty(this.SelectedProcess.Path))
            {
                return;
            }

            this.isBlockOperationRunning = true;
            this.ToggleBlockCommand.NotifyCanExecuteChanged();

            try
            {
                var dirPath = System.IO.Path.GetDirectoryName(this.SelectedProcess.Path)!;
                var appName = System.IO.Path.GetFileName(dirPath);

                if (this.IsSelectedProcessBlocked)
                {
                    this.loggingService.LogInfo($"Unblocking application {appName}...");
                    await this.firewallService.RemoveExistingRules(appName, this.loggingService);
                }
                else
                {
                    this.loggingService.LogInfo($"Blocking application {appName}...");
                    await this.firewallService.BlockApplicationFiles(
                        dirPath,
                        blockExe: true,
                        blockDll: false,
                        includeSubdirectories: false,
                        excludedKeywords: new System.Collections.Generic.List<string>(),
                        excludedFiles: new System.Collections.Generic.List<string>(),
                        logger: this.loggingService,
                        cancellationToken: System.Threading.CancellationToken.None);
                }
            }
            finally
            {
                this.isBlockOperationRunning = false;
                await this.UpdateBlockedStatusAsync();
                this.ToggleBlockCommand.NotifyCanExecuteChanged();
            }
        }
    }
}