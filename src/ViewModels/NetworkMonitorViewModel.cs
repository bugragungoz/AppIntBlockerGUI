namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// ViewModel that exposes live per-process network statistics to the view.
    /// </summary>
    public sealed partial class NetworkMonitorViewModel : ObservableObject, IDisposable
    {
        private readonly INetworkMonitorService networkMonitorService;
        private bool monitoringStarted;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkMonitorViewModel"/> class.
        /// </summary>
        public NetworkMonitorViewModel(INetworkMonitorService networkMonitorService)
        {
            this.networkMonitorService = networkMonitorService;
        }

        /// <summary>
        /// Gets the live collection of network usage objects bound to the UI.
        /// </summary>
        public ObservableCollection<ProcessNetworkUsageModel> Usages => this.networkMonitorService.Usages;

        [ObservableProperty]
        private ProcessNetworkUsageModel? selectedProcess;

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
    }
}