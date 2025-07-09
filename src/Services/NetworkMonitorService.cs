namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;
    using PacketDotNet;
    using SharpPcap;
    using SharpPcap.LibPcap;

    public sealed class NetworkMonitorService : INetworkMonitorService, IDisposable
    {
        private readonly ILoggingService loggingService;
        private readonly ObservableCollection<ProcessNetworkUsageModel> usages = new();
        private readonly ConcurrentDictionary<int, ProcessNetworkUsageModel?> usageLookup = new();
        private ConcurrentDictionary<string, int> connectionToPidMap = new();

        private CancellationTokenSource? cancellationTokenSource;
        private Task? monitoringTask;
        private ICaptureDevice? captureDevice;
        private string? selectedDeviceName;
        private System.Timers.Timer? updateTimer;
        private System.Timers.Timer? pidMappingTimer;
        private long totalBytesSent = 0;
        private long totalBytesReceived = 0;
        private DateTime lastSampleTime;

        public NetworkMonitorService(ILoggingService loggingService)
        {
            this.loggingService = loggingService;
            loggingService.LogInfo("NetworkMonitorService (SharpPcap Real-Time) created.");
        }

        public ObservableCollection<ProcessNetworkUsageModel> Usages => this.usages;

        public IReadOnlyList<string> GetAvailableDevices()
        {
            try
            {
                return CaptureDeviceList.Instance
                    .Where(d => d is LibPcapLiveDevice)
                    .Select(d => d.Description)
                    .ToList();
            }
            catch (Exception ex)
            {
                loggingService.LogError("Could not retrieve list of network devices.", ex);
                return new List<string>();
            }
        }

        public void StartMonitoring(string deviceName, int intervalMilliseconds = 1000)
        {
            if (monitoringTask != null && !monitoringTask.IsCompleted)
            {
                if (selectedDeviceName == deviceName)
                {
                    loggingService.LogWarning("Monitoring is already active on the selected device.");
                    return;
                }
                
                // If a different device is selected, stop the current monitoring first.
                StopMonitoringAsync().Wait();
            }

            selectedDeviceName = deviceName;
            loggingService.LogInfo($"Starting network monitoring on {deviceName}...");
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            monitoringTask = Task.Run(() => RunMonitoring(token), token);

            updateTimer = new System.Timers.Timer(intervalMilliseconds);
            updateTimer.Elapsed += UpdateUi;
            updateTimer.AutoReset = true;
            updateTimer.Start();

            pidMappingTimer = new System.Timers.Timer(5000); // Refresh PID map every 5 seconds
            pidMappingTimer.Elapsed += (s, e) => UpdateConnectionToPidMap();
            pidMappingTimer.AutoReset = true;
            pidMappingTimer.Start();
        }

        private void RunMonitoring(CancellationToken token)
        {
            try
            {
                captureDevice = CaptureDeviceList.Instance.FirstOrDefault(d => d.Description == selectedDeviceName);

                if (captureDevice == null)
                {
                    loggingService.LogError($"The selected network device '{selectedDeviceName}' was not found.");
                    return;
                }

                captureDevice.OnPacketArrival += OnPacketArrival;
                captureDevice.Open(DeviceModes.Promiscuous, 1000);
                
                loggingService.LogInfo($"Started capturing on: {captureDevice.Description}");
                
                UpdateConnectionToPidMap();
                lastSampleTime = DateTime.UtcNow;

                captureDevice.StartCapture();

                token.WaitHandle.WaitOne();

                loggingService.LogInfo("Stopping capture.");
                captureDevice.StopCapture();
                captureDevice.Close();
            }
            catch (Exception ex)
            {
                loggingService.LogError("An error occurred during network monitoring.", ex);
            }
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            try
            {
                var packet = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.Data.ToArray());
                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket == null) return;

                int packetLength = ipPacket.TotalLength;
                bool isUpload = IsUpload(ipPacket.SourceAddress);

                if (isUpload) Interlocked.Add(ref totalBytesSent, packetLength);
                else Interlocked.Add(ref totalBytesReceived, packetLength);

                int pid = 0;
                if (ipPacket.PayloadPacket is TcpPacket tcpPacket)
                {
                    string key = $"{ipPacket.SourceAddress}:{tcpPacket.SourcePort}-{ipPacket.DestinationAddress}:{tcpPacket.DestinationPort}";
                    string reverseKey = $"{ipPacket.DestinationAddress}:{tcpPacket.DestinationPort}-{ipPacket.SourceAddress}:{tcpPacket.SourcePort}";
                    connectionToPidMap.TryGetValue(key, out pid);
                    if (pid == 0) connectionToPidMap.TryGetValue(reverseKey, out pid);
                }
                else if (ipPacket.PayloadPacket is UdpPacket udpPacket)
                {
                    string key = $"{ipPacket.SourceAddress}:{udpPacket.SourcePort}-{ipPacket.DestinationAddress}:{udpPacket.DestinationPort}";
                    string reverseKey = $"{ipPacket.DestinationAddress}:{udpPacket.DestinationPort}-{ipPacket.SourceAddress}:{udpPacket.SourcePort}";
                    connectionToPidMap.TryGetValue(key, out pid);
                    if (pid == 0) connectionToPidMap.TryGetValue(reverseKey, out pid);
                }
                
                if (pid > 0)
                {
                    var usage = usageLookup.GetOrAdd(pid, p =>
                    {
                        try
                        {
                            var process = Process.GetProcessById(p);
                            return new ProcessNetworkUsageModel { ProcessId = p, ProcessName = process.ProcessName, Path = process.MainModule?.FileName ?? "N/A" };
                        }
                        catch { return null; }
                    });
                    
                    if (usage != null)
                    {
                        if (isUpload) Interlocked.Add(ref usage.TotalSentBytes, packetLength);
                        else Interlocked.Add(ref usage.TotalReceivedBytes, packetLength);
                    }
                }
            }
            catch (Exception ex)
            {
                loggingService.LogDebug($"Packet processing error: {ex.Message}");
            }
        }
        
        private bool IsUpload(IPAddress sourceAddress)
        {
             if (captureDevice is not LibPcapLiveDevice liveDevice) return false;
             return liveDevice.Addresses.Any(a => a.Addr?.ipAddress?.Equals(sourceAddress) ?? false);
        }

        private void UpdateUi(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var now = DateTime.UtcNow;
            var timeDiff = (now - lastSampleTime).TotalSeconds;
            if (timeDiff < 0.1) return;

            var currentSent = Interlocked.Read(ref totalBytesSent);
            var currentReceived = Interlocked.Read(ref totalBytesReceived);
            
            // This is a simplified total, not per process.
            // For per-process bandwidth, we would need to store previous byte counts.

            lastSampleTime = now;

            foreach (var usage in usageLookup.Values)
            {
                 if (usage != null)
                 {
                    // This calculation is complex to do accurately in real-time without more state.
                    // For now, let's keep it simple and just update totals.
                    usage.LastUpdatedUtc = now;
                 }
            }

            SyncObservableCollection();
        }

        private void UpdateConnectionToPidMap()
        {
            loggingService.LogDebug("Updating TCP/UDP connection to PID map.");
            var newMap = new ConcurrentDictionary<string, int>();

            try
            {
                foreach (var row in GetTcpConnections())
                {
                    string key = $"{row.LocalAddress}:{row.LocalPort}-{row.RemoteAddress}:{row.RemotePort}";
                    newMap[key] = row.OwningPid;
                }

                foreach (var row in GetUdpConnections())
                {
                    string key = $"{row.LocalAddress}:{row.LocalPort}-0.0.0.0:0"; // UDP is connectionless
                    newMap[key] = row.OwningPid;
                }

                connectionToPidMap = newMap;
            }
            catch (Exception ex)
            {
                loggingService.LogError("Failed to update connection-to-PID map.", ex);
            }
        }
        
        #region IPHLPAPI P/Invoke for PID mapping
        // ... TCP ...
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, TcpTableClass tableClass, uint reserved = 0);

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcpRowOwnerPid
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] remotePort;
            public uint owningPid;
        }

        private enum TcpTableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }
        
        // ... UDP ...
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder, int ulAf, UdpTableClass tableClass, uint reserved = 0);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct MibUdpRowOwnerPid
        {
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public int owningPid;
        }

        private enum UdpTableClass
        {
            UdpTableBasic,
            UdpTableOwnerPid,
            UdpTableOwnerModule
        }

        private static ProcessTcpConnection[] GetTcpConnections()
        {
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll);
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll) != 0)
                    return Array.Empty<ProcessTcpConnection>();

                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                var connections = new ProcessTcpConnection[rowCount];

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                    connections[i] = new ProcessTcpConnection(
                        new IPAddress(row.localAddr), BitConverter.ToUInt16(row.localPort.Reverse().ToArray(), 0),
                        new IPAddress(row.remoteAddr), BitConverter.ToUInt16(row.remotePort.Reverse().ToArray(), 0),
                        (int)row.owningPid);
                    rowPtr = IntPtr.Add(rowPtr, Marshal.SizeOf<MibTcpRowOwnerPid>());
                }
                return connections;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        
        private static ProcessUdpConnection[] GetUdpConnections()
        {
            int bufferSize = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, 2, UdpTableClass.UdpTableOwnerPid);
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedUdpTable(buffer, ref bufferSize, true, 2, UdpTableClass.UdpTableOwnerPid) != 0)
                    return Array.Empty<ProcessUdpConnection>();
                
                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                var connections = new ProcessUdpConnection[rowCount];

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPtr);
                    connections[i] = new ProcessUdpConnection(
                        new IPAddress(row.localAddr), BitConverter.ToUInt16(new byte[] { row.localPort[1], row.localPort[0] }, 0),
                        row.owningPid
                    );
                    rowPtr = IntPtr.Add(rowPtr, Marshal.SizeOf<MibUdpRowOwnerPid>());
                }
                return connections;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        
        // Helper classes to hold connection info
        private record ProcessTcpConnection(IPAddress LocalAddress, ushort LocalPort, IPAddress RemoteAddress, ushort RemotePort, int OwningPid);
        private record ProcessUdpConnection(IPAddress LocalAddress, ushort LocalPort, int OwningPid);
        
        #endregion

        private void SyncObservableCollection()
        {
            var activeProcesses = new HashSet<ProcessNetworkUsageModel>(
                usageLookup.Values.Where(p => p != null && (p.TotalSentBytes > 0 || p.TotalReceivedBytes > 0)).OfType<ProcessNetworkUsageModel>());

            if (System.Windows.Application.Current?.Dispatcher == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var existingProcessIds = new HashSet<int>(this.usages.Select(p => p.ProcessId));
                var activeProcessIds = new HashSet<int>(activeProcesses.Select(p => p.ProcessId));

                // Remove old
                var toRemove = this.usages.Where(existing => !activeProcessIds.Contains(existing.ProcessId)).ToList();
                foreach (var old in toRemove)
                {
                    this.usages.Remove(old);
                }

                // Add new
                var toAdd = activeProcesses.Where(active => !existingProcessIds.Contains(active.ProcessId)).ToList();
                foreach (var newItem in toAdd)
                {
                    this.usages.Add(newItem);
                }
            });
        }

        public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            loggingService.LogInfo("Stopping network monitoring...");
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (monitoringTask != null)
            {
                await Task.WhenAny(monitoringTask, Task.Delay(-1, cancellationToken));
                monitoringTask = null;
            }
            
            updateTimer?.Stop();
            updateTimer?.Dispose();
            pidMappingTimer?.Stop();
            pidMappingTimer?.Dispose();
        }

        public void Dispose()
        {
            StopMonitoringAsync().Wait();
        }
    }
}