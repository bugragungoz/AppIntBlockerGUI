namespace AppIntBlockerGUI.Models
{
    using System;

    /// <summary>
    /// Holds per-process network usage statistics gathered by <see cref="INetworkMonitorService"/>.
    /// </summary>
    public class ProcessNetworkUsageModel
    {
        /// <summary>
        /// Gets or sets the ID of the process.
        /// </summary>
        public int ProcessId { get; init; }

        /// <summary>
        /// Gets or sets the display name of the process.
        /// </summary>
        public string ProcessName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path to the executable.
        /// </summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the cumulative number of bytes sent by this process since the service started monitoring.
        /// </summary>
        public long TotalSentBytes { get; set; }

        /// <summary>
        /// Gets or sets the cumulative number of bytes received by this process since the service started monitoring.
        /// </summary>
        public long TotalReceivedBytes { get; set; }

        /// <summary>
        /// Gets or sets the upload rate (kbit/s) calculated in the last sampling interval.
        /// </summary>
        public double UploadKbps { get; set; }

        /// <summary>
        /// Gets or sets the download rate (kbit/s) calculated in the last sampling interval.
        /// </summary>
        public double DownloadKbps { get; set; }

        /// <summary>
        /// Gets the timestamp of the last sample when this model was updated.
        /// </summary>
        public DateTime LastUpdatedUtc { get; internal set; } = DateTime.UtcNow;

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