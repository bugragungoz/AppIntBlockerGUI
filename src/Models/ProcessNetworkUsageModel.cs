namespace AppIntBlockerGUI.Models
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// Holds per-process network usage statistics gathered by <see cref="INetworkMonitorService"/>.
    /// </summary>
    public partial class ProcessNetworkUsageModel : ObservableObject
    {
        [ObservableProperty]
        private int processId;

        [ObservableProperty]
        private string processName = string.Empty;

        [ObservableProperty]
        private string path = string.Empty;

        public long TotalSentBytes;
        public long TotalReceivedBytes;

        [ObservableProperty]
        private double uploadKbps;

        [ObservableProperty]
        private double downloadKbps;

        [ObservableProperty]
        private DateTime lastUpdatedUtc;

        /// <summary>
        /// Gets the total sent data in megabytes.
        /// </summary>
        public double TotalSentMB => this.TotalSentBytes / 1048576.0;

        /// <summary>
        /// Gets the total received data in megabytes.
        /// </summary>
        public double TotalReceivedMB => this.TotalReceivedBytes / 1048576.0;
    }
}