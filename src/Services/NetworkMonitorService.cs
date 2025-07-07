namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;

    /// <summary>
    /// Default implementation of <see cref="INetworkMonitorService"/>. For the first iteration it uses
    /// <see cref="PerformanceCounter"/> instances to sample per-process network I/O every N milliseconds.
    /// The heavy-lifting (ETW, IP-helper, etc.) can be substituted later without changing the public surface.
    /// </summary>
    public sealed class NetworkMonitorService : INetworkMonitorService, IDisposable
    {
        private readonly ILoggingService loggingService;
        private readonly ObservableCollection<ProcessNetworkUsageModel> usages = new();
        private readonly ConcurrentDictionary<int, ProcessNetworkUsageModel> usageLookup = new();
        private Timer? timer;

        public NetworkMonitorService(ILoggingService loggingService)
        {
            this.loggingService = loggingService;
        }

        /// <inheritdoc />
        public ObservableCollection<ProcessNetworkUsageModel> Usages => this.usages;

        /// <inheritdoc />
        public void StartMonitoring(int intervalMilliseconds = 1000)
        {
            if (this.timer != null)
            {
                return; // already running
            }

            this.timer = new Timer(this.SampleNetworkUsage, null, 0, intervalMilliseconds);
        }

        /// <inheritdoc />
        public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            var currentTimer = this.timer;
            if (currentTimer != null)
            {
                this.timer = null;
                await Task.Run(() => currentTimer.Dispose(), cancellationToken).ConfigureAwait(false);
            }
        }

        private void SampleNetworkUsage(object? state)
        {
            try
            {
                // NOTE: This is a placeholder implementation that only enumerates running processes.
                // Actual byte counters will be filled in subsequent iterations.

                var processes = Process.GetProcesses();
                var seenPids = new ConcurrentDictionary<int, bool>();

                foreach (var process in processes)
                {
                    seenPids.TryAdd(process.Id, true);
                    var model = this.usageLookup.GetOrAdd(process.Id, pid =>
                    {
                        var exePath = string.Empty;
                        try
                        {
                            exePath = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            // Access denied for some system processes – ignore.
                        }

                        return new ProcessNetworkUsageModel
                        {
                            ProcessId = pid,
                            ProcessName = process.ProcessName,
                            Path = exePath,
                        };
                    });

                    // TODO: Collect byte counters – placeholder zeros for now.
                    model.LastUpdatedUtc = DateTime.UtcNow;
                }

                // Remove processes that have exited
                var outdated = this.usageLookup.Keys.Except(seenPids.Keys).ToList();
                foreach (var pid in outdated)
                {
                    this.usageLookup.TryRemove(pid, out _);
                }

                // Sync with observable collection on UI thread (if available)
                this.SyncObservableCollection();
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Network monitoring sample error: {ex.Message}", ex);
            }
        }

        private void SyncObservableCollection()
        {
            // WPF bindings require updates on UI dispatcher if we are not already on it.
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(this.SyncObservableCollection);
                return;
            }

            // Simple replace-all strategy for initial implementation.
            this.usages.Clear();
            foreach (var model in this.usageLookup.Values.OrderByDescending(u => u.DownloadKbps + u.UploadKbps))
            {
                this.usages.Add(model);
            }
        }

        public void Dispose()
        {
            this.timer?.Dispose();
        }
    }
}