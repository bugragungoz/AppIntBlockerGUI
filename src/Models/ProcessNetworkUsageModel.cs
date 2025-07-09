namespace AppIntBlockerGUI.Models
{
    using System;
    using System.Threading;
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

        private long _totalSentBytes;
        private long _totalReceivedBytes;

        public long TotalSentBytes => _totalSentBytes;
        public long TotalReceivedBytes => _totalReceivedBytes;

        public void AddSentBytes(long bytes)
        {
            Interlocked.Add(ref _totalSentBytes, bytes);
            OnPropertyChanged(nameof(TotalSentBytes));
            OnPropertyChanged(nameof(TotalSentMB));
        }

        public void AddReceivedBytes(long bytes)
        {
            Interlocked.Add(ref _totalReceivedBytes, bytes);
            OnPropertyChanged(nameof(TotalReceivedBytes));
            OnPropertyChanged(nameof(TotalReceivedMB));
        }

        public long PreviousTotalSentBytes;
        public long PreviousTotalReceivedBytes;
        public DateTime PreviousSampleTime;

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