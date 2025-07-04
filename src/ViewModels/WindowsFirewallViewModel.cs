using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;
using AppIntBlockerGUI.Models;
using System.Management.Automation;
using System.Diagnostics;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class WindowsFirewallViewModel : ObservableObject
    {
        private readonly IFirewallService _firewallService;
        private readonly IDialogService _dialogService;
        private readonly ILoggingService _loggingService;

        [ObservableProperty]
        private ObservableCollection<FirewallRuleModel> allRules = new();

        [ObservableProperty]
        private ICollectionView? filteredRules;

        [ObservableProperty]
        private FirewallRuleModel? selectedRule;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string selectedDirection = "All";

        [ObservableProperty]
        private string selectedAction = "All";

        [ObservableProperty]
        private string selectedSource = "All";

        [ObservableProperty]
        private int totalRules = 0;

        [ObservableProperty]
        private int inboundRules = 0;

        [ObservableProperty]
        private int outboundRules = 0;

        [ObservableProperty]
        private int appIntBlockerRules = 0;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private DateTime lastRefreshTime = DateTime.Now;

        public WindowsFirewallViewModel()
        {
            _firewallService = new FirewallService();
            _dialogService = new DialogService();
            _loggingService = new LoggingService();

            // Initialize filtered view
            FilteredRules = CollectionViewSource.GetDefaultView(AllRules);
            FilteredRules.Filter = ApplyFilters;

            // Load rules on initialization
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            StatusMessage = "Initializing firewall rules...";
            await LoadFirewallRulesAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredRules?.Refresh();
        }

        partial void OnSelectedDirectionChanged(string value)
        {
            FilteredRules?.Refresh();
        }

        partial void OnSelectedActionChanged(string value)
        {
            FilteredRules?.Refresh();
        }

        partial void OnSelectedSourceChanged(string value)
        {
            FilteredRules?.Refresh();
        }

        private bool ApplyFilters(object item)
        {
            if (item is not FirewallRuleModel rule) return false;

            // Search text filter
            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchLower = SearchText.ToLower();
                if (!rule.DisplayName.ToLower().Contains(searchLower) &&
                    !rule.ProgramPath.ToLower().Contains(searchLower) &&
                    !rule.Description.ToLower().Contains(searchLower))
                {
                    return false;
                }
            }

            // Direction filter
            if (SelectedDirection != "All" && rule.Direction != SelectedDirection)
            {
                return false;
            }

            // Action filter
            if (SelectedAction != "All" && rule.Action != SelectedAction)
            {
                return false;
            }

            // Source filter
            if (SelectedSource != "All")
            {
                var ruleType = rule.RuleType;
                if (SelectedSource != ruleType)
                {
                    return false;
                }
            }

            return true;
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadFirewallRulesAsync();
        }

        private async Task LoadFirewallRulesAsync()
        {
            try
            {
                StatusMessage = "Loading firewall rules...";
                _loggingService.LogInfo("Loading all Windows Firewall rules...");

                var rules = await Task.Run(async () =>
                {
                    try
                    {
                        return await GetAllFirewallRulesAsync();
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError("Background error loading firewall rules", ex);
                        return new List<FirewallRuleModel>();
                    }
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    AllRules.Clear();
                    foreach (var rule in rules.OrderBy(r => r.DisplayName))
                    {
                        AllRules.Add(rule);
                    }

                    UpdateStatistics();
                    FilteredRules?.Refresh();
                    LastRefreshTime = DateTime.Now;
                    StatusMessage = $"Loaded {TotalRules} firewall rules";
                });

                _loggingService.LogInfo($"Successfully loaded {TotalRules} firewall rules, {AppIntBlockerRules} created by AppIntBlocker");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error loading firewall rules", ex);
                StatusMessage = "Error loading firewall rules";
                _dialogService.ShowMessage("Failed to load firewall rules. Check the log for details.", "Error");
            }
        }

        private async Task<List<FirewallRuleModel>> GetAllFirewallRulesAsync()
        {
            var rules = new List<FirewallRuleModel>();

            try
            {
                using (var powerShell = PowerShell.Create())
                {
                    // Get all firewall rules with detailed information
                    powerShell.AddCommand("Get-NetFirewallRule")
                        .AddParameter("ErrorAction", "SilentlyContinue");

                    var psResults = await Task.Run(() => powerShell.Invoke());

                    foreach (var psObject in psResults)
                    {
                        try
                        {
                            var rule = new FirewallRuleModel();

                            // Basic properties
                            rule.DisplayName = psObject.Properties["DisplayName"]?.Value?.ToString() ?? "Unknown";
                            rule.RuleName = psObject.Properties["Name"]?.Value?.ToString() ?? "";
                            rule.Direction = psObject.Properties["Direction"]?.Value?.ToString() ?? "";
                            rule.Action = psObject.Properties["Action"]?.Value?.ToString() ?? "";
                            rule.Protocol = psObject.Properties["Protocol"]?.Value?.ToString() ?? "";
                            rule.Profile = psObject.Properties["Profile"]?.Value?.ToString() ?? "";
                            rule.Description = psObject.Properties["Description"]?.Value?.ToString() ?? "";
                            rule.Group = psObject.Properties["Group"]?.Value?.ToString() ?? "";
                            
                            // Enabled status
                            if (bool.TryParse(psObject.Properties["Enabled"]?.Value?.ToString(), out bool enabled))
                            {
                                rule.Enabled = enabled;
                                rule.IsEnabled = enabled;
                            }

                            rule.Status = rule.Enabled ? "Enabled" : "Disabled";

                            // Check if it's an AppIntBlocker rule
                            rule.IsAppIntBlockerRule = rule.DisplayName.StartsWith("AppBlocker Rule", StringComparison.OrdinalIgnoreCase);

                            // Try to get additional details with Get-NetFirewallApplicationFilter
                            try
                            {
                                using (var appFilterPS = PowerShell.Create())
                                {
                                    appFilterPS.AddCommand("Get-NetFirewallApplicationFilter")
                                        .AddParameter("AssociatedNetFirewallRule", psObject)
                                        .AddParameter("ErrorAction", "SilentlyContinue");

                                    var appResults = appFilterPS.Invoke();
                                    if (appResults.Any())
                                    {
                                        var appFilter = appResults.First();
                                        rule.ProgramPath = appFilter.Properties["Program"]?.Value?.ToString() ?? "";
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore errors when getting application filter
                            }

                            // Try to get port information
                            try
                            {
                                using (var portFilterPS = PowerShell.Create())
                                {
                                    portFilterPS.AddCommand("Get-NetFirewallPortFilter")
                                        .AddParameter("AssociatedNetFirewallRule", psObject)
                                        .AddParameter("ErrorAction", "SilentlyContinue");

                                    var portResults = portFilterPS.Invoke();
                                    if (portResults.Any())
                                    {
                                        var portFilter = portResults.First();
                                        rule.LocalPort = portFilter.Properties["LocalPort"]?.Value?.ToString() ?? "";
                                        rule.RemotePort = portFilter.Properties["RemotePort"]?.Value?.ToString() ?? "";
                                        
                                        // Override protocol if available
                                        var protocol = portFilter.Properties["Protocol"]?.Value?.ToString();
                                        if (!string.IsNullOrEmpty(protocol))
                                        {
                                            rule.Protocol = protocol;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore errors when getting port filter
                            }

                            rules.Add(rule);
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogWarning($"Error processing individual firewall rule: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error getting firewall rules with PowerShell", ex);
                
                // Try netsh fallback
                return await GetFirewallRulesWithNetshAsync();
            }

            return rules;
        }

        private async Task<List<FirewallRuleModel>> GetFirewallRulesWithNetshAsync()
        {
            var rules = new List<FirewallRuleModel>();

            try
            {
                _loggingService.LogInfo("Using netsh fallback to get firewall rules...");

                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=all verbose",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        await Task.Run(() => process.WaitForExit());

                        // Parse netsh output (simplified)
                        var lines = output.Split('\n');
                        FirewallRuleModel? currentRule = null;

                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();

                            if (trimmedLine.StartsWith("Rule Name:"))
                            {
                                if (currentRule != null)
                                {
                                    rules.Add(currentRule);
                                }

                                currentRule = new FirewallRuleModel();
                                currentRule.DisplayName = trimmedLine.Substring("Rule Name:".Length).Trim();
                                currentRule.RuleName = currentRule.DisplayName;
                                currentRule.IsAppIntBlockerRule = currentRule.DisplayName.StartsWith("AppBlocker Rule", StringComparison.OrdinalIgnoreCase);
                            }
                            else if (currentRule != null)
                            {
                                if (trimmedLine.StartsWith("Direction:"))
                                {
                                    currentRule.Direction = trimmedLine.Substring("Direction:".Length).Trim();
                                }
                                else if (trimmedLine.StartsWith("Action:"))
                                {
                                    currentRule.Action = trimmedLine.Substring("Action:".Length).Trim();
                                }
                                else if (trimmedLine.StartsWith("Enabled:"))
                                {
                                    var enabledText = trimmedLine.Substring("Enabled:".Length).Trim();
                                    currentRule.Enabled = enabledText.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                    currentRule.IsEnabled = currentRule.Enabled;
                                    currentRule.Status = currentRule.Enabled ? "Enabled" : "Disabled";
                                }
                                else if (trimmedLine.StartsWith("Protocol:"))
                                {
                                    currentRule.Protocol = trimmedLine.Substring("Protocol:".Length).Trim();
                                }
                                else if (trimmedLine.StartsWith("Program:"))
                                {
                                    currentRule.ProgramPath = trimmedLine.Substring("Program:".Length).Trim();
                                }
                            }
                        }

                        // Add the last rule
                        if (currentRule != null)
                        {
                            rules.Add(currentRule);
                        }
                    }
                }

                _loggingService.LogInfo($"Loaded {rules.Count} firewall rules using netsh fallback");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("netsh fallback also failed", ex);
            }

            return rules;
        }

        private void UpdateStatistics()
        {
            TotalRules = AllRules.Count;
            InboundRules = AllRules.Count(r => r.Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase));
            OutboundRules = AllRules.Count(r => r.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase));
            AppIntBlockerRules = AllRules.Count(r => r.IsAppIntBlockerRule);
        }

        [RelayCommand]
        private void OpenFirewallConsole()
        {
            try
            {
                StatusMessage = "Opening Windows Firewall Console...";
                _loggingService.LogInfo("Opening Windows Firewall Console (wf.msc)");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "wf.msc",
                    UseShellExecute = true
                });

                StatusMessage = "Windows Firewall Console opened";
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error opening Windows Firewall Console", ex);
                StatusMessage = "Error opening firewall console";
                _dialogService.ShowMessage("Failed to open Windows Firewall Console. Make sure you have administrator privileges.", "Error");
            }
        }

        [RelayCommand]
        private async Task ToggleRule(FirewallRuleModel rule)
        {
            if (rule == null) return;

            try
            {
                StatusMessage = $"Toggling rule: {rule.DisplayName}";
                _loggingService.LogInfo($"Toggling firewall rule: {rule.DisplayName} (Currently: {rule.Status})");

                var newState = !rule.Enabled;
                var action = newState ? "enable" : "disable";

                var success = await Task.Run(async () =>
                {
                    try
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"advfirewall firewall set rule name={EscapeNetshArgument(rule.DisplayName)} new enable={action}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(processInfo))
                        {
                            if (process != null)
                            {
                                await Task.Run(() => process.WaitForExit());
                                return process.ExitCode == 0;
                            }
                        }

                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (success)
                {
                    rule.Enabled = newState;
                    rule.IsEnabled = newState;
                    rule.Status = newState ? "Enabled" : "Disabled";
                    StatusMessage = $"Rule {action}d successfully";
                    FilteredRules?.Refresh();
                }
                else
                {
                    StatusMessage = $"Failed to {action} rule";
                    _dialogService.ShowMessage($"Failed to {action} the firewall rule. Check the log for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error toggling firewall rule: {rule.DisplayName}", ex);
                StatusMessage = "Error toggling rule";
                _dialogService.ShowMessage("An error occurred while toggling the rule. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task DeleteRule(FirewallRuleModel rule)
        {
            if (rule == null) return;

            try
            {
                var confirmMessage = $"Delete firewall rule?\n\n" +
                                   $"• Name: {rule.DisplayName}\n" +
                                   $"• Direction: {rule.Direction}\n" +
                                   $"• Action: {rule.Action}\n" +
                                   $"• Status: {rule.Status}\n\n" +
                                   $"This action cannot be undone. Continue?";

                if (!_dialogService.ShowConfirmation(confirmMessage, "Delete Firewall Rule"))
                {
                    return;
                }

                StatusMessage = $"Deleting rule: {rule.DisplayName}";
                _loggingService.LogInfo($"Deleting firewall rule: {rule.DisplayName}");

                var success = await Task.Run(async () =>
                {
                    try
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"advfirewall firewall delete rule name={EscapeNetshArgument(rule.DisplayName)}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(processInfo))
                        {
                            if (process != null)
                            {
                                await Task.Run(() => process.WaitForExit());
                                return process.ExitCode == 0;
                            }
                        }

                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (success)
                {
                    AllRules.Remove(rule);
                    UpdateStatistics();
                    StatusMessage = "Rule deleted successfully";
                    _dialogService.ShowMessage("Firewall rule deleted successfully.", "Rule Deleted");
                }
                else
                {
                    StatusMessage = "Failed to delete rule";
                    _dialogService.ShowMessage("Failed to delete the firewall rule. Check the log for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error deleting firewall rule: {rule.DisplayName}", ex);
                StatusMessage = "Error deleting rule";
                _dialogService.ShowMessage("An error occurred while deleting the rule. Check the log for details.", "Error");
            }
        }

        /// <summary>
        /// Securely escapes arguments for netsh commands to prevent command injection.
        /// AI-generated code: This method implements proper escaping to prevent security vulnerabilities.
        /// </summary>
        private static string EscapeNetshArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";
                
            // Escape quotes by doubling them and wrap in quotes
            return "\"" + argument.Replace("\"", "\"\"") + "\"";
        }
    }
} 