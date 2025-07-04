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
    using System.Threading.Tasks;

    public class SystemRestoreService : ISystemRestoreService
    {
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
            return await Task.Run(() =>
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemRestore"))
                    {
                        // If we can query without errors, System Restore is likely enabled
                        var results = searcher.Get();
                        return results != null;
                    }
                }
                catch
                {
                    // If we get an exception, System Restore might be disabled
                    return false;
                }
            });
        }

        public async Task<List<RestorePoint>> GetRestorePointsAsync()
        {
            return await Task.Run(() =>
            {
                var restorePoints = new List<RestorePoint>();

                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemRestore"))
                    {
                        var results = searcher.Get();

                        foreach (ManagementObject result in results)
                        {
                            try
                            {
                                var restorePoint = new RestorePoint();

                                // Get sequence number
                                if (uint.TryParse(result["SequenceNumber"]?.ToString(), out uint seqNum))
                                {
                                    restorePoint.SequenceNumber = seqNum;
                                }

                                // Get description
                                restorePoint.Description = result["Description"]?.ToString() ?? "Unknown";

                                // Get creation time
                                var creationTimeStr = result["CreationTime"]?.ToString();
                                if (!string.IsNullOrEmpty(creationTimeStr))
                                {
                                    // WMI datetime format: YYYYMMDDHHMMSS.mmmmmm+UUU
                                    // CRITICAL FIX: Validate length before substring
                                    if (creationTimeStr.Length >= 14)
                                    {
                                        if (DateTime.TryParseExact(
                                            creationTimeStr.Substring(0, 14),
                                            "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime creationTime))
                                        {
                                            restorePoint.CreationTime = creationTime;
                                        }
                                        else
                                        {
                                            restorePoint.CreationTime = DateTime.Now;
                                        }
                                    }
                                    else
                                    {
                                        // Fallback: try to parse the full string
                                        if (DateTime.TryParse(creationTimeStr, out DateTime fallbackTime))
                                        {
                                            restorePoint.CreationTime = fallbackTime;
                                        }
                                        else
                                        {
                                            restorePoint.CreationTime = DateTime.Now;
                                        }
                                    }
                                }
                                else
                                {
                                    restorePoint.CreationTime = DateTime.Now;
                                }

                                // Get restore point type
                                var restoreTypeCode = result["RestorePointType"]?.ToString();
                                restorePoint.RestorePointType = this.GetRestorePointTypeDescription(restoreTypeCode);

                                // Get event type
                                var eventTypeCode = result["EventType"]?.ToString();
                                restorePoint.EventType = this.GetEventTypeDescription(eventTypeCode);

                                restorePoints.Add(restorePoint);
                            }
                            catch (Exception ex)
                            {
                                // Log individual restore point processing errors but continue
                                Debug.WriteLine($"Error processing restore point: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting restore points: {ex.Message}");
                }

                return restorePoints.OrderByDescending(rp => rp.CreationTime).ToList();
            });
        }

        public async Task<bool> CreateRestorePointAsync(string description)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use WMI to create a restore point
                    var scope = new ManagementScope(@"\\.\root\default");
                    var path = new ManagementPath("SystemRestore");
                    var options = new ObjectGetOptions();
                    var classObject = new ManagementClass(scope, path, options);

                    var inParams = classObject.GetMethodParameters("CreateRestorePoint");
                    inParams["Description"] = description;
                    inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                    inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE

                    var outParams = classObject.InvokeMethod("CreateRestorePoint", inParams, null);

                    // Check return value (0 = success)
                    var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
                    return returnValue == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating restore point: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> RestoreSystemAsync(uint sequenceNumber)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use WMI to restore system to a specific restore point
                    var scope = new ManagementScope(@"\\.\root\default");
                    var path = new ManagementPath("SystemRestore");
                    var options = new ObjectGetOptions();
                    var classObject = new ManagementClass(scope, path, options);

                    var inParams = classObject.GetMethodParameters("Restore");
                    inParams["SequenceNumber"] = sequenceNumber;

                    var outParams = classObject.InvokeMethod("Restore", inParams, null);

                    // Check return value (0 = success)
                    var returnValue = Convert.ToInt32(outParams["ReturnValue"]);
                    return returnValue == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error restoring system: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteRestorePointAsync(uint sequenceNumber)
        {
            // Note: Windows does not provide a direct WMI method to delete specific restore points
            // This method exists for interface compatibility but will return false
            return await Task.Run(() =>
            {
                try
                {
                    // Windows doesn't provide an easy way to delete specific restore points via WMI
                    // The user would need to use Disk Cleanup or vssadmin commands
                    return false;
                }
                catch
                {
                    return false;
                }
            });
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
