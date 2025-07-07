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
    using System.Collections.Generic;

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
        private readonly ConcurrentDictionary<int, CounterPair> counterLookup = new();
        private static readonly ConcurrentDictionary<int, string?> InstanceNameCache = new();
        private Timer? timer;
        private int intervalMilliseconds;
        private double IntervalSeconds => this.intervalMilliseconds / 1000.0;

        private sealed class CounterPair : IDisposable
        {
            public CounterPair(PerformanceCounter read, PerformanceCounter write)
            {
                this.Read = read;
                this.Write = write;
            }

            public PerformanceCounter Read { get; }
            public PerformanceCounter Write { get; }

            public void Dispose()
            {
                this.Read.Dispose();
                this.Write.Dispose();
            }
        }

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

            this.intervalMilliseconds = intervalMilliseconds;
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
                var processes = Process.GetProcesses();
                var seenPids = new HashSet<int>();

                foreach (var process in processes)
                {
                    seenPids.Add(process.Id);

                    var model = this.usageLookup.GetOrAdd(process.Id, pid =>
                    {
                        var path = string.Empty;
                        try
                        {
                            path = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                        }

                        return new ProcessNetworkUsageModel
                        {
                            ProcessId = pid,
                            ProcessName = process.ProcessName,
                            Path = path,
                        };
                    });

                    // Ensure we have counters for this process
                    var cp = this.counterLookup.GetOrAdd(process.Id, pid => this.TryCreateCountersForProcess(pid));
                    if (cp == null)
                    {
                        continue; // Could not create counters (system process etc.)
                    }

                    double readBytesPerSec = 0;
                    double writeBytesPerSec = 0;
                    try
                    {
                        readBytesPerSec = cp.Read.NextValue();
                        writeBytesPerSec = cp.Write.NextValue();
                    }
                    catch (Exception ex)
                    {
                        this.loggingService.LogWarning($"Failed to read counter for PID {process.Id}: {ex.Message}");
                    }

                    // Convert to kilobits per second
                    model.DownloadKbps = readBytesPerSec * 8 / 1000.0;
                    model.UploadKbps = writeBytesPerSec * 8 / 1000.0;

                    // Accumulate totals
                    model.TotalReceivedBytes += (long)(readBytesPerSec * this.IntervalSeconds);
                    model.TotalSentBytes += (long)(writeBytesPerSec * this.IntervalSeconds);

                    model.LastUpdatedUtc = DateTime.UtcNow;
                }

                // Cleanup for processes that have exited
                var removedPids = this.usageLookup.Keys.Except(seenPids).ToList();
                foreach (var pid in removedPids)
                {
                    this.usageLookup.TryRemove(pid, out _);
                    if (this.counterLookup.TryRemove(pid, out var counters))
                    {
                        counters?.Dispose();
                    }
                }

                this.SyncObservableCollection();
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Network monitoring sample error: {ex.Message}", ex);
            }
        }

        private CounterPair? TryCreateCountersForProcess(int pid)
        {
            try
            {
                var instance = InstanceNameCache.GetOrAdd(pid, id => GetInstanceNameForProcess(id));
                if (string.IsNullOrEmpty(instance))
                {
                    return null;
                }

                var read = new PerformanceCounter("Process", "IO Read Bytes/sec", instance, true);
                var write = new PerformanceCounter("Process", "IO Write Bytes/sec", instance, true);

                // Warm-up first value to avoid spikes
                _ = read.NextValue();
                _ = write.NextValue();

                return new CounterPair(read, write);
            }
            catch (Exception ex)
            {
                this.loggingService.LogWarning($"Failed to create performance counters for PID {pid}: {ex.Message}");
                return null;
            }
        }

        private static string? GetInstanceNameForProcess(int pid)
        {
            try
            {
                var category = new PerformanceCounterCategory("Process");
                foreach (var instance in category.GetInstanceNames())
                {
                    using var counter = new PerformanceCounter("Process", "ID Process", instance, true);
                    try
                    {
                        if ((int)counter.RawValue == pid)
                        {
                            return instance;
                        }
                    }
                    catch
                    {
                        // Ignore and continue
                    }
                }
            }
            catch
            {
            }

            return null;
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
            foreach (var kvp in this.counterLookup)
            {
                kvp.Value?.Dispose();
            }
        }
    }
}