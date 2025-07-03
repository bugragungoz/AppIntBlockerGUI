using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;
using AppIntBlockerGUI.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Management.Automation;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class ManageRulesViewModel : ObservableObject
    {
        private readonly IFirewallService _firewallService;
        private readonly ILoggingService _loggingService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<FirewallRuleModel> allRules = new();

        [ObservableProperty]
        private ObservableCollection<FirewallRuleModel> filteredFirewallRules = new();

        [ObservableProperty]
        private FirewallRuleModel? selectedRule;

        [ObservableProperty]
        private string searchFilter = string.Empty;

        [ObservableProperty]
        private string operationLog = string.Empty;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int totalRulesCount;

        [ObservableProperty]
        private int inboundRulesCount;

        [ObservableProperty]
        private int outboundRulesCount;

        [ObservableProperty]
        private bool hasSelectedRule;

        public ManageRulesViewModel()
        {
            _firewallService = new FirewallService();
            _loggingService = new LoggingService();
            _dialogService = new DialogService();

            // Subscribe to logging events
            _loggingService.LogEntryAdded += OnLogEntryAdded;

            _loggingService.LogInfo("ManageRulesViewModel initialized - ready for manual refresh");

            // Don't auto-refresh in constructor to prevent crashes
            // Refresh will be triggered when view becomes visible
        }

        partial void OnSearchFilterChanged(string value)
        {
            FilterRules();
        }

        partial void OnSelectedRuleChanged(FirewallRuleModel? value)
        {
            HasSelectedRule = value != null;
        }

        private void OnLogEntryAdded(string logEntry)
        {
            // Update UI on main thread
            App.Current?.Dispatcher.Invoke(() =>
            {
                OperationLog += logEntry + "\n";
                
                // Keep log from getting too long
                var lines = OperationLog.Split('\n');
                if (lines.Length > 100)
                {
                    OperationLog = string.Join("\n", lines.Skip(lines.Length - 100));
                }
            });
        }

        private void FilterRules()
        {
            if (string.IsNullOrWhiteSpace(SearchFilter))
            {
                FilteredFirewallRules = new ObservableCollection<FirewallRuleModel>(AllRules);
            }
            else
            {
                var filtered = AllRules.Where(rule =>
                    rule.RuleName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    rule.ApplicationName.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    rule.Direction.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                FilteredFirewallRules = new ObservableCollection<FirewallRuleModel>(filtered);
            }
        }

        private void UpdateStatistics()
        {
            TotalRulesCount = AllRules.Count;
            InboundRulesCount = AllRules.Count(r => r.Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase));
            OutboundRulesCount = AllRules.Count(r => r.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase));
        }

        // Removed GetAppIntBlockerRulesOnly() to eliminate duplication
        // FirewallService.GetExistingRules() already filters for AppIntBlocker rules

        [RelayCommand]
        public async Task RefreshRules()
        {
            await RefreshRulesAsync();
        }

        private async Task RefreshRulesAsync()
        {
            try
            {
                IsLoading = true;
                _loggingService.LogInfo("Refreshing AppIntBlocker firewall rules...");

                // Run the rules gathering in background to prevent UI blocking
                var rules = await Task.Run(async () =>
                {
                    try
                    {
                        // Get AppIntBlocker rules using FirewallService (already filtered)
                        var existingRuleNames = await _firewallService.GetExistingRulesAsync(_loggingService);
                        
                        var rulesList = new List<FirewallRuleModel>();

                        foreach (var ruleName in existingRuleNames)
                        {
                            try
                            {
                                // Parse rule information from name
                                var rule = ParseRuleFromName(ruleName);
                                if (rule != null)
                                {
                                    rulesList.Add(rule);
                                }
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogError($"Error parsing rule: {ruleName}", ex);
                            }
                        }

                        return rulesList.OrderBy(r => r.ApplicationName).ThenBy(r => r.RuleName).ToList();
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError("Background error refreshing firewall rules", ex);
                        return new List<FirewallRuleModel>();
                    }
                });

                // Update UI on main thread
                AllRules = new ObservableCollection<FirewallRuleModel>(rules);
                FilterRules();
                UpdateStatistics();

                _loggingService.LogInfo($"Successfully loaded {AllRules.Count} AppIntBlocker firewall rules");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error refreshing firewall rules", ex);
                _dialogService.ShowMessage("Failed to refresh firewall rules. Check the log for details.", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private FirewallRuleModel? ParseRuleFromName(string ruleName)
        {
            try
            {
                // Expected format: "AppBlocker Rule - ApplicationName - FileName (Direction)"
                if (string.IsNullOrEmpty(ruleName) || !ruleName.StartsWith("AppBlocker Rule - "))
                    return null;

                // Split by " - " but be careful about file names that might contain dashes
                var parts = ruleName.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    _loggingService.LogWarning($"Invalid rule format: {ruleName}");
                    return null;
                }

                var applicationName = parts[1].Trim();
                
                // Reconstruct the file and direction part (in case filename had dashes)
                var fileAndDirection = string.Join(" - ", parts.Skip(2));

                // Extract direction from parentheses at the end
                var directionStart = fileAndDirection.LastIndexOf('(');
                var directionEnd = fileAndDirection.LastIndexOf(')');
                
                if (directionStart == -1 || directionEnd == -1 || directionEnd != fileAndDirection.Length - 1)
                {
                    _loggingService.LogWarning($"Could not parse direction from rule: {ruleName}");
                    return null;
                }

                var direction = fileAndDirection.Substring(directionStart + 1, directionEnd - directionStart - 1).Trim();
                var fileName = fileAndDirection.Substring(0, directionStart).Trim();

                // Validate direction
                if (!direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase) && 
                    !direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase))
                {
                    _loggingService.LogWarning($"Invalid direction '{direction}' in rule: {ruleName}");
                    return null;
                }

                return new FirewallRuleModel
                {
                    RuleName = ruleName,
                    ApplicationName = applicationName,
                    Direction = direction,
                    Status = "Enabled", // Rules we can retrieve are enabled
                    ProgramPath = fileName
                };
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error parsing rule name: {ruleName}", ex);
                return null;
            }
        }

        [RelayCommand]
        private async Task RemoveSelectedRule()
        {
            if (SelectedRule == null)
            {
                _dialogService.ShowMessage("Please select a rule to remove.", "No Rule Selected");
                return;
            }

            var confirmMessage = $"Are you sure you want to remove the following rule?\n\n" +
                               $"Rule: {SelectedRule.RuleName}\n" +
                               $"Application: {SelectedRule.ApplicationName}\n" +
                               $"Direction: {SelectedRule.Direction}\n\n" +
                               $"Note: This will remove ONLY this specific rule.";

            if (!_dialogService.ShowConfirmation(confirmMessage, "Confirm Single Rule Removal"))
            {
                _loggingService.LogInfo("Single rule removal cancelled by user");
                return;
            }

            var ruleToRemove = SelectedRule.RuleName; // Store before async operation

            try
            {
                _loggingService.LogInfo($"Removing single rule: {ruleToRemove}");

                // Run the removal operation in background to prevent UI blocking
                var success = await Task.Run(async () => 
                {
                    try
                    {
                        return await _firewallService.RemoveSingleRule(ruleToRemove, _loggingService);
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Background task error removing rule: {ruleToRemove}", ex);
                        return false;
                    }
                });

                if (success)
                {
                    _loggingService.LogInfo($"Successfully removed rule: {ruleToRemove}");
                    await RefreshRulesAsync();
                    _dialogService.ShowMessage($"Successfully removed rule:\n{ruleToRemove}", "Rule Removed");
                }
                else
                {
                    _loggingService.LogError($"Failed to remove rule: {ruleToRemove}");
                    _dialogService.ShowMessage("Failed to remove the selected rule. Check the log for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error removing rule: {ruleToRemove}", ex);
                _dialogService.ShowMessage("An error occurred while removing the rule. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task RemoveAllRules()
        {
            if (AllRules.Count == 0)
            {
                _dialogService.ShowMessage("No rules found to remove.", "No Rules");
                return;
            }

            var confirmMessage = $"This will remove ALL {AllRules.Count} AppIntBlocker firewall rules.\n\n" +
                               "This action cannot be undone. Are you sure you want to continue?";

            if (!_dialogService.ShowConfirmation(confirmMessage, "Confirm Remove All Rules"))
            {
                _loggingService.LogInfo("Remove all rules operation cancelled by user");
                return;
            }

            try
            {
                _loggingService.LogInfo("Starting removal of all AppIntBlocker rules...");

                // Get unique application names
                var applicationNames = AllRules.Select(r => r.ApplicationName).Distinct().ToList();

                // Run removal operations in background
                var (successCount, errorCount) = await Task.Run(async () =>
                {
                    int success = 0;
                    int errors = 0;

                    foreach (var appName in applicationNames)
                    {
                        try
                        {
                            var result = await _firewallService.RemoveExistingRules(appName, _loggingService);
                            if (result)
                            {
                                success++;
                            }
                            else
                            {
                                errors++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            _loggingService.LogError($"Error removing rules for {appName}", ex);
                        }
                    }

                    return (success, errors);
                });

                _loggingService.LogInfo($"Remove all operation completed. Success: {successCount}, Errors: {errorCount}");

                if (errorCount > 0)
                {
                    _dialogService.ShowMessage($"Completed with {errorCount} errors. Check the log for details.", "Partial Success");
                }
                else
                {
                    _dialogService.ShowMessage("Successfully removed all AppIntBlocker rules.", "Success");
                }

                await RefreshRulesAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error during remove all rules operation", ex);
                _dialogService.ShowMessage("An error occurred while removing rules. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task ExportRules()
        {
            try
            {
                if (AllRules.Count == 0)
                {
                    _dialogService.ShowMessage("No rules to export.", "No Data");
                    return;
                }

                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var fileName = $"AppIntBlocker_Rules_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(documentsPath, fileName);

                var exportContent = new List<string>
                {
                    "AppIntBlocker Firewall Rules Export",
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Total Rules: {AllRules.Count}",
                    "",
                    "Rules Details:",
                    "".PadRight(80, '=')
                };

                foreach (var rule in AllRules.OrderBy(r => r.ApplicationName).ThenBy(r => r.RuleName))
                {
                    exportContent.Add($"Rule Name: {rule.RuleName}");
                    exportContent.Add($"Application: {rule.ApplicationName}");
                    exportContent.Add($"Direction: {rule.Direction}");
                    exportContent.Add($"Status: {rule.Status}");
                    exportContent.Add($"Program: {rule.ProgramPath}");
                    exportContent.Add("");
                }

                await File.WriteAllLinesAsync(filePath, exportContent);

                _loggingService.LogInfo($"Rules exported to: {filePath}");
                _dialogService.ShowMessage($"Rules successfully exported to:\n{filePath}", "Export Complete");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error exporting rules", ex);
                _dialogService.ShowMessage("Failed to export rules. Check the log for details.", "Export Error");
            }
        }

        [RelayCommand]
        private void ImportRules()
        {
            try
            {
                _loggingService.LogInfo("Opening import file dialog...");
                
                // Create an OpenFileDialog (this would normally be in DialogService)
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Firewall Rules",
                    Filter = "XML Files (*.xml)|*.xml|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = ".xml"
                };

                if (dialog.ShowDialog() == true)
                {
                    var fileName = dialog.FileName;
                    _loggingService.LogInfo($"Importing rules from: {fileName}");
                    
                    // For now, just log the import action
                    // Future implementation would parse and import the rules
                    _dialogService.ShowMessage("Import feature coming soon!", "Import Rules");
                    
                    _loggingService.LogInfo("Import operation completed");
                }
                else
                {
                    _loggingService.LogInfo("Import operation cancelled by user");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error importing rules", ex);
                _dialogService.ShowMessage("Failed to import rules. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            OperationLog = string.Empty;
            _loggingService.LogInfo("Operation log cleared by user");
        }
    }
} 