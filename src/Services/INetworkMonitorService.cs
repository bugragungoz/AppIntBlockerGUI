namespace AppIntBlockerGUI.Services
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;

    /// <summary>
    /// Provides live per-process network usage data that can be bound to the UI.
    /// </summary>
    public interface INetworkMonitorService
    {
        /// <summary>
        /// Gets an observable collection containing the latest usage statistics for all running processes.
        /// </summary>
        ObservableCollection<ProcessNetworkUsageModel> Usages { get; }

        /// <summary>
        /// Gets a list of available network devices for monitoring.
        /// </summary>
        IReadOnlyList<string> GetAvailableDevices();

        /// <summary>
        /// Starts background monitoring on a specific device. Calling multiple times has no effect if monitoring is already active.
        /// </summary>
        /// <param name="deviceName">The friendly name of the device to monitor.</param>
        /// <param name="intervalMilliseconds">Sampling interval in milliseconds.</param>
        Task StartMonitoringAsync(string deviceName, int intervalMilliseconds = 1000);

        /// <summary>
        /// Stops the background monitoring thread and releases all unmanaged resources.
        /// </summary>
        Task StopMonitoringAsync(CancellationToken cancellationToken = default);
    }
}