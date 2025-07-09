namespace AppIntBlockerGUI.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// Enhanced process network usage statistics with service detection and traffic analysis.
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
        /// Enhanced network service information
        /// </summary>
        [ObservableProperty]
        private List<NetworkServiceInfo> detectedServices = new();

        /// <summary>
        /// Primary detected service for this process
        /// </summary>
        public NetworkServiceInfo? PrimaryService => DetectedServices?.FirstOrDefault();

        /// <summary>
        /// Primary service name for display
        /// </summary>
        public string ServiceName => PrimaryService?.ServiceName ?? "Unknown";

        /// <summary>
        /// Service category for grouping
        /// </summary>
        public string ServiceCategory => PrimaryService?.Category ?? "Unknown";

        /// <summary>
        /// Network connections details
        /// </summary>
        [ObservableProperty]
        private int activeConnectionsCount;

        [ObservableProperty]
        private int incomingConnectionsCount;

        [ObservableProperty]
        private int outgoingConnectionsCount;

        [ObservableProperty]
        private int localConnectionsCount;

        /// <summary>
        /// Traffic classification
        /// </summary>
        [ObservableProperty]
        private bool hasIncomingTraffic;

        [ObservableProperty]
        private bool hasOutgoingTraffic;

        [ObservableProperty]
        private bool hasLocalTraffic;

        /// <summary>
        /// Security analysis
        /// </summary>
        [ObservableProperty]
        private bool hasSecuritySensitiveConnections;

        [ObservableProperty]
        private bool hasSystemServiceConnections;

        [ObservableProperty]
        private bool isSystemProcess;

        /// <summary>
        /// Remote connections summary
        /// </summary>
        [ObservableProperty]
        private List<string> remoteAddresses = new();

        [ObservableProperty]
        private List<string> remoteDomains = new();

        /// <summary>
        /// Protocol statistics
        /// </summary>
        [ObservableProperty]
        private int tcpConnectionsCount;

        [ObservableProperty]
        private int udpConnectionsCount;

        [ObservableProperty]
        private int icmpPacketsCount;

        /// <summary>
        /// Enhanced traffic analysis
        /// </summary>
        public string TrafficDirectionSummary
        {
            get
            {
                var directions = new List<string>();
                if (HasIncomingTraffic) directions.Add("⬇️ IN");
                if (HasOutgoingTraffic) directions.Add("⬆️ OUT");
                if (HasLocalTraffic) directions.Add("🔄 LOCAL");
                return directions.Any() ? string.Join(" ", directions) : "📵 NONE";
            }
        }

        /// <summary>
        /// Connection type summary for display
        /// </summary>
        public string ConnectionSummary
        {
            get
            {
                var parts = new List<string>();
                if (TcpConnectionsCount > 0) parts.Add($"TCP:{TcpConnectionsCount}");
                if (UdpConnectionsCount > 0) parts.Add($"UDP:{UdpConnectionsCount}");
                if (IcmpPacketsCount > 0) parts.Add($"ICMP:{IcmpPacketsCount}");
                return parts.Any() ? string.Join(" | ", parts) : "No Connections";
            }
        }

        /// <summary>
        /// Security status indicator
        /// </summary>
        public string SecurityStatus
        {
            get
            {
                if (HasSecuritySensitiveConnections) return "🔒 Security Sensitive";
                if (HasSystemServiceConnections) return "🪟 System Service";
                if (IsSystemProcess) return "🪟 System Process";
                return "👤 User Process";
            }
        }

        /// <summary>
        /// Primary remote destination for display
        /// </summary>
        public string PrimaryDestination
        {
            get
            {
                if (RemoteDomains?.Any() == true)
                    return RemoteDomains.First();
                if (RemoteAddresses?.Any() == true)
                    return RemoteAddresses.First();
                return "Local/Unknown";
            }
        }

        /// <summary>
        /// Gets the total sent data in megabytes.
        /// </summary>
        public double TotalSentMB => this.TotalSentBytes / 1048576.0;

        /// <summary>
        /// Gets the total received data in megabytes.
        /// </summary>
        public double TotalReceivedMB => this.TotalReceivedBytes / 1048576.0;

        /// <summary>
        /// Gets formatted sent rate in MB/s (Megabytes per second).
        /// </summary>
        public string SentRateFormatted 
        {
            get
            {
                // Convert from kbps to MB/s: kbps -> bps -> MB/s
                var mbps = (UploadKbps * 1024) / (8 * 1024 * 1024); // kbps to MB/s
                return $"{mbps:F2} MB/s";
            }
        }

        /// <summary>
        /// Gets formatted received rate in MB/s (Megabytes per second).
        /// </summary>
        public string ReceivedRateFormatted 
        {
            get
            {
                // Convert from kbps to MB/s: kbps -> bps -> MB/s
                var mbps = (DownloadKbps * 1024) / (8 * 1024 * 1024); // kbps to MB/s
                return $"{mbps:F2} MB/s";
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

        /// <summary>
        /// Updates network intelligence data for this process
        /// </summary>
        public void UpdateNetworkIntelligence(List<NetworkServiceInfo> services, 
            Dictionary<string, int> connectionStats,
            Dictionary<string, List<string>> remoteConnections)
        {
            // Update detected services
            DetectedServices = services ?? new List<NetworkServiceInfo>();

            // Update connection statistics
            if (connectionStats != null)
            {
                TcpConnectionsCount = connectionStats.GetValueOrDefault("tcp", 0);
                UdpConnectionsCount = connectionStats.GetValueOrDefault("udp", 0);
                IcmpPacketsCount = connectionStats.GetValueOrDefault("icmp", 0);
                
                IncomingConnectionsCount = connectionStats.GetValueOrDefault("incoming", 0);
                OutgoingConnectionsCount = connectionStats.GetValueOrDefault("outgoing", 0);
                LocalConnectionsCount = connectionStats.GetValueOrDefault("local", 0);
                
                ActiveConnectionsCount = TcpConnectionsCount + UdpConnectionsCount;
            }

            // Update traffic direction flags
            HasIncomingTraffic = IncomingConnectionsCount > 0;
            HasOutgoingTraffic = OutgoingConnectionsCount > 0;
            HasLocalTraffic = LocalConnectionsCount > 0;

            // Update security analysis
            HasSecuritySensitiveConnections = DetectedServices.Any(s => s.IsSecuritySensitive);
            HasSystemServiceConnections = DetectedServices.Any(s => s.IsSystemService);

            // Update remote connection data
            if (remoteConnections != null)
            {
                RemoteAddresses = remoteConnections.GetValueOrDefault("addresses", new List<string>());
                RemoteDomains = remoteConnections.GetValueOrDefault("domains", new List<string>());
            }

            // Trigger property change notifications for computed properties
            OnPropertyChanged(nameof(ServiceName));
            OnPropertyChanged(nameof(ServiceCategory));
            OnPropertyChanged(nameof(TrafficDirectionSummary));
            OnPropertyChanged(nameof(ConnectionSummary));
            OnPropertyChanged(nameof(SecurityStatus));
            OnPropertyChanged(nameof(PrimaryDestination));
            OnPropertyChanged(nameof(PrimaryService));
        }

        /// <summary>
        /// Adds a detected service to this process
        /// </summary>
        public void AddDetectedService(NetworkServiceInfo service)
        {
            if (service == null) return;

            var existingService = DetectedServices.FirstOrDefault(s => 
                s.Port == service.Port && s.Protocol == service.Protocol);

            if (existingService == null)
            {
                DetectedServices.Add(service);
                OnPropertyChanged(nameof(DetectedServices));
                OnPropertyChanged(nameof(PrimaryService));
                OnPropertyChanged(nameof(ServiceName));
                OnPropertyChanged(nameof(ServiceCategory));
            }
        }

        /// <summary>
        /// Updates traffic statistics and rates
        /// </summary>
        public void UpdateTrafficRates(double uploadKbps, double downloadKbps)
        {
            UploadKbps = uploadKbps;
            DownloadKbps = downloadKbps;
            LastUpdatedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks this as a system process for security analysis
        /// </summary>
        public void SetSystemProcess(bool isSystem)
        {
            IsSystemProcess = isSystem;
            OnPropertyChanged(nameof(SecurityStatus));
        }
    }
}