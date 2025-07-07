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
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = this.networkMonitorService.StopMonitoringAsync();
        }

        partial void OnSelectedProcessChanged(ProcessNetworkUsageModel? value)
        {
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