// <copyright file="SystemRestoreService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class SystemRestoreService : ISystemRestoreService
    {
        private readonly ILoggingService loggingService;

        public SystemRestoreService(ILoggingService loggingService)
        {
            this.loggingService = loggingService;
        }

        public class RestorePoint
        {
            public uint SequenceNumber { get; set; }

            public string Description { get; set; } = string.Empty;

            public DateTime CreationTime { get; set; }

            public string RestorePointType { get; set; } = string.Empty;

            public string EventType { get; set; } = string.Empty;
        }

        public async Task<bool> IsSystemRestoreEnabledAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemRestore"))
                    {
                        var results = searcher.Get();
                        return results != null && results.Count > 0;
                    }
                });
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("System Restore might be disabled or an error occurred.", ex);
                return false;
            }
        }

        public async Task<List<RestorePoint>> GetRestorePointsAsync()
        {
            var restorePoints = new List<RestorePoint>();
            try
            {
                await Task.Run(() =>
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemRestore"))
                    {
                        foreach (ManagementObject result in searcher.Get())
                        {
                            try
                            {
                                restorePoints.Add(new RestorePoint
                                {
                                    SequenceNumber = (uint)result["SequenceNumber"],
                                    Description = result["Description"]?.ToString() ?? "Unknown",
                                    CreationTime = ManagementDateTimeConverter.ToDateTime(result["CreationTime"]?.ToString() ?? string.Empty),
                                    RestorePointType = this.GetRestorePointTypeDescription(result["RestorePointType"]?.ToString()),
                                    EventType = this.GetEventTypeDescription(result["EventType"]?.ToString()),
                                });
                            }
                            catch (Exception ex)
                            {
                                this.loggingService.LogWarning($"Error processing a restore point, skipping. Details: {ex.Message}");
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Failed to retrieve system restore points.", ex);
            }
            return restorePoints.OrderByDescending(rp => rp.CreationTime).ToList();
        }

        public async Task<bool> CreateRestorePointAsync(string description)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var scope = new ManagementScope(@"\\.\root\default");
                    var path = new ManagementPath("SystemRestore");
                    var classObject = new ManagementClass(scope, path, new ObjectGetOptions());

                    var inParams = classObject.GetMethodParameters("CreateRestorePoint");
                    inParams["Description"] = description;
                    inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                    inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE

                    var outParams = classObject.InvokeMethod("CreateRestorePoint", inParams, null);
                    var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
                    if (returnValue != 0)
                    {
                        this.loggingService.LogWarning($"WMI CreateRestorePoint returned non-zero value: {returnValue}");
                    }
                    return returnValue == 0;
                });
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Error creating restore point: {description}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreSystemAsync(uint sequenceNumber)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var scope = new ManagementScope(@"\\.\root\default");
                    var path = new ManagementPath("SystemRestore");
                    var classObject = new ManagementClass(scope, path, new ObjectGetOptions());

                    var inParams = classObject.GetMethodParameters("Restore");
                    inParams["SequenceNumber"] = sequenceNumber;

                    var outParams = classObject.InvokeMethod("Restore", inParams, null);
                    var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
                    if (returnValue != 0)
                    {
                        this.loggingService.LogWarning($"WMI Restore returned non-zero value: {returnValue}");
                    }
                    return returnValue == 0;
                });
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Error restoring system to restore point {sequenceNumber}", ex);
                return false;
            }
        }

        [Obsolete("Windows does not provide a direct WMI/API method to delete specific restore points.")]
        public Task<bool> DeleteRestorePointAsync(uint sequenceNumber)
        {
            this.loggingService.LogWarning("DeleteRestorePointAsync was called, but this feature is not supported by Windows. Returning false.");
            return Task.FromResult(false);
        }

        private string GetRestorePointTypeDescription(string? typeCode)
        {
            return typeCode switch
            {
                "0" => "Application Install",
                "1" => "Application Uninstall",
                "10" => "Device Driver Install",
                "12" => "Modify Settings",
                "13" => "Cancelled Operation",
                _ => "Unknown"
            };
        }

        private string GetEventTypeDescription(string? eventCode)
        {
            return eventCode switch
            {
                "100" => "Begin System Change",
                "101" => "End System Change",
                "102" => "Begin Nested System Change",
                "103" => "End Nested System Change",
                _ => "Unknown"
            };
        }

        public async Task<int> GetRestorePointCountAsync()
        {
            var restorePoints = await this.GetRestorePointsAsync();
            return restorePoints.Count;
        }

        public async Task<int> GetAppIntBlockerRestorePointCountAsync()
        {
            var restorePoints = await this.GetRestorePointsAsync();
            return restorePoints.Count(rp => rp.Description.Contains("AppIntBlocker", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<string> GetSystemRestoreStatusAsync()
        {
            var isEnabled = await this.IsSystemRestoreEnabledAsync();
            return isEnabled ? "System Restore is enabled" : "System Restore is disabled";
        }
    }
}
