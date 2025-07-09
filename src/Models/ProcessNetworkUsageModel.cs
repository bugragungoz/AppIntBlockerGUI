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
            OnPropertyChanged(nameof(TotalSentFormatted));
        }

        public void AddReceivedBytes(long bytes)
        {
            Interlocked.Add(ref _totalReceivedBytes, bytes);
            OnPropertyChanged(nameof(TotalReceivedBytes));
            OnPropertyChanged(nameof(TotalReceivedMB));
            OnPropertyChanged(nameof(TotalReceivedFormatted));
        }

        public long PreviousTotalSentBytes;
        public long PreviousTotalReceivedBytes;
        public DateTime PreviousSampleTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SentRateFormatted))]
        private double uploadKbps;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReceivedRateFormatted))]
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

        /// <summary>
        /// Gets formatted sent rate with appropriate units (kbit/s or Mbit/s).
        /// </summary>
        public string SentRateFormatted 
        {
            get
            {
                var kbps = UploadKbps * 1024; // Convert from Mbps to kbps
                if (kbps < 1000)
                    return $"{kbps:F0} kbit/s";
                else
                    return $"{UploadKbps:F2} Mbit/s";
            }
        }

        /// <summary>
        /// Gets formatted received rate with appropriate units (kbit/s or Mbit/s).
        /// </summary>
        public string ReceivedRateFormatted 
        {
            get
            {
                var kbps = DownloadKbps * 1024; // Convert from Mbps to kbps
                if (kbps < 1000)
                    return $"{kbps:F0} kbit/s";
                else
                    return $"{DownloadKbps:F2} Mbit/s";
            }
        }

        /// <summary>
        /// Gets formatted total sent data with appropriate units.
        /// </summary>
        public string TotalSentFormatted 
        {
            get
            {
                if (TotalSentMB < 1)
                    return $"{TotalSentBytes / 1024.0:F0} KB";
                else if (TotalSentMB < 1024)
                    return $"{TotalSentMB:F1} MB";
                else
                    return $"{TotalSentMB / 1024.0:F2} GB";
            }
        }

        /// <summary>
        /// Gets formatted total received data with appropriate units.
        /// </summary>
        public string TotalReceivedFormatted 
        {
            get
            {
                if (TotalReceivedMB < 1)
                    return $"{TotalReceivedBytes / 1024.0:F0} KB";
                else if (TotalReceivedMB < 1024)
                    return $"{TotalReceivedMB:F1} MB";
                else
                    return $"{TotalReceivedMB / 1024.0:F2} GB";
            }
        }
    }
}