// <copyright file="ManageRulesViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Models;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public partial class ManageRulesViewModel : ObservableObject, IDisposable
    {
        private readonly IFirewallService firewallService;
        private readonly ILoggingService loggingService;
        private readonly IDialogService dialogService;

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

        public ManageRulesViewModel(
            IFirewallService firewallService,
            ILoggingService loggingService,
            IDialogService dialogService)
        {
            this.firewallService = firewallService ?? throw new ArgumentNullException(nameof(firewallService));
            this.loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            this.dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // Subscribe to logging events
            this.loggingService.LogEntryAdded += this.OnLogEntryAdded;

            this.loggingService.LogInfo("ManageRulesViewModel initialized - ready for manual refresh");

            // Don't auto-refresh in constructor to prevent crashes
            // Refresh will be triggered when view becomes visible
        }

        partial void OnSearchFilterChanged(string value)
        {
            this.FilterRules();
        }

        partial void OnSelectedRuleChanged(FirewallRuleModel? value)
        {
            this.HasSelectedRule = value != null;
        }

        private void OnLogEntryAdded(string logEntry)
        {
            // Update UI on main thread
            App.Current?.Dispatcher.Invoke(() =>
            {
                this.OperationLog += logEntry + "\n";

                // Keep log from getting too long
                var lines = this.OperationLog.Split('\n');
                if (lines.Length > 100)
                {
                    this.OperationLog = string.Join("\n", lines.Skip(lines.Length - 100));
                }
            });
        }

        private void FilterRules()
        {
            if (string.IsNullOrWhiteSpace(this.SearchFilter))
            {
                this.FilteredFirewallRules = new ObservableCollection<FirewallRuleModel>(this.AllRules);
            }
            else
            {
                var filtered = this.AllRules.Where(rule =>
                    rule.RuleName.Contains(this.SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    rule.ApplicationName.Contains(this.SearchFilter, StringComparison.OrdinalIgnoreCase) ||
                    rule.Direction.Contains(this.SearchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

                this.FilteredFirewallRules = new ObservableCollection<FirewallRuleModel>(filtered);
            }
        }

        private void UpdateStatistics()
        {
            this.TotalRulesCount = this.AllRules.Count;
            this.InboundRulesCount = this.AllRules.Count(r => r.Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase));
            this.OutboundRulesCount = this.AllRules.Count(r => r.Direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase));
        }

        // Removed GetAppIntBlockerRulesOnly() to eliminate duplication
        // FirewallService.GetExistingRules() already filters for AppIntBlocker rules
        [RelayCommand]
        public async Task RefreshRules()
        {
            await this.RefreshRulesAsync();
        }

        private async Task RefreshRulesAsync()
        {
            try
            {
                this.IsLoading = true;
                this.loggingService.LogInfo("Refreshing AppIntBlocker firewall rules...");

                // Run the rules gathering in background to prevent UI blocking
                var rules = await Task.Run(async () =>
                {
                    try
                    {
                        // Get AppIntBlocker rules using FirewallService (already filtered)
                        var existingRuleNames = await this.firewallService.GetExistingRulesAsync(this.loggingService);

                        var rulesList = new List<FirewallRuleModel>();

                        foreach (var ruleName in existingRuleNames)
                        {
                            try
                            {
                                // Parse rule information from name
                                var rule = this.ParseRuleFromName(ruleName);
                                if (rule != null)
                                {
                                    rulesList.Add(rule);
                                }
                            }
                            catch (Exception ex)
                            {
                                this.loggingService.LogError($"Error parsing rule: {ruleName}", ex);
                            }
                        }

                        return rulesList.OrderBy(r => r.ApplicationName).ThenBy(r => r.RuleName).ToList();
                    }
                    catch (Exception ex)
                    {
                        this.loggingService.LogError("Background error refreshing firewall rules", ex);
                        return new List<FirewallRuleModel>();
                    }
                });

                // Update UI on main thread
                this.AllRules = new ObservableCollection<FirewallRuleModel>(rules);
                this.FilterRules();
                this.UpdateStatistics();

                this.loggingService.LogInfo($"Successfully loaded {this.AllRules.Count} AppIntBlocker firewall rules");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error refreshing firewall rules", ex);
                this.dialogService.ShowMessage("Failed to refresh firewall rules. Check the log for details.", "Error");
            }
            finally
            {
                this.IsLoading = false;
            }
        }

        private FirewallRuleModel? ParseRuleFromName(string ruleName)
        {
            try
            {
                // ROBUST PARSING with validation
                if (string.IsNullOrWhiteSpace(ruleName))
                {
                    this.loggingService.LogWarning("Cannot parse null or empty rule name");
                    return null;
                }

                const string expectedPrefix = "AppBlocker Rule - ";
                if (!ruleName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    this.loggingService.LogDebug($"Rule does not match expected format: {ruleName}");
                    return null;
                }

                // Remove prefix
                var nameWithoutPrefix = ruleName.Substring(expectedPrefix.Length);

                // Validate minimum length
                if (nameWithoutPrefix.Length < 10) // Minimum reasonable length
                {
                    this.loggingService.LogWarning($"Rule name too short after prefix removal: {ruleName}");
                    return null;
                }

                // Look for direction pattern at the end: " (Inbound)" or " (Outbound)"
                var directionPattern = @"\s+\((Inbound|Outbound)\)$";
                var directionMatch = System.Text.RegularExpressions.Regex.Match(nameWithoutPrefix, directionPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!directionMatch.Success)
                {
                    this.loggingService.LogWarning($"Could not parse direction from rule: {ruleName}");
                    return null;
                }

                var direction = directionMatch.Groups[1].Value;
                var nameWithoutDirection = nameWithoutPrefix.Substring(0, directionMatch.Index);

                // Split by " - " to get application and file
                var parts = nameWithoutDirection.Split(new[] { " - " }, 2, StringSplitOptions.None);

                if (parts.Length != 2)
                {
                    this.loggingService.LogWarning($"Invalid rule format, expected 'App - File': {ruleName}");
                    return null;
                }

                var applicationName = parts[0].Trim();
                var fileName = parts[1].Trim();

                // Validate components
                if (string.IsNullOrWhiteSpace(applicationName) || string.IsNullOrWhiteSpace(fileName))
                {
                    this.loggingService.LogWarning($"Empty application or file name in rule: {ruleName}");
                    return null;
                }

                return new FirewallRuleModel
                {
                    RuleName = ruleName,
                    ApplicationName = applicationName,
                    Direction = direction,
                    Status = "Enabled",
                    ProgramPath = fileName,
                    DisplayName = ruleName
                };
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Exception parsing rule name: {ruleName}", ex);
                return null;
            }
        }

        [RelayCommand]
        private async Task RemoveSelectedRule()
        {
            if (this.SelectedRule == null)
            {
                this.dialogService.ShowMessage("Please select a rule to remove.", "No Rule Selected");
                return;
            }

            var confirmMessage = $"Are you sure you want to remove the following rule?\n\n" +
                               $"Rule: {this.SelectedRule.RuleName}\n" +
                               $"Application: {this.SelectedRule.ApplicationName}\n" +
                               $"Direction: {this.SelectedRule.Direction}\n\n" +
                               $"Note: This will remove ONLY this specific rule.";

            if (!this.dialogService.ShowConfirmation(confirmMessage, "Confirm Single Rule Removal"))
            {
                this.loggingService.LogInfo("Single rule removal cancelled by user");
                return;
            }

            var ruleToRemove = this.SelectedRule.RuleName; // Store before async operation

            try
            {
                this.loggingService.LogInfo($"Removing single rule: {ruleToRemove}");

                // Run the removal operation in background to prevent UI blocking
                var success = await Task.Run(async () =>
                {
                    try
                    {
                        return await this.firewallService.RemoveSingleRule(ruleToRemove, this.loggingService);
                    }
                    catch (Exception ex)
                    {
                        this.loggingService.LogError($"Background task error removing rule: {ruleToRemove}", ex);
                        return false;
                    }
                });

                if (success)
                {
                    this.loggingService.LogInfo($"Successfully removed rule: {ruleToRemove}");
                    await this.RefreshRulesAsync();
                    this.dialogService.ShowMessage($"Successfully removed rule:\n{ruleToRemove}", "Rule Removed");
                }
                else
                {
                    this.loggingService.LogError($"Failed to remove rule: {ruleToRemove}");
                    this.dialogService.ShowMessage("Failed to remove the selected rule. Check the log for details.", "Error");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Error removing rule: {ruleToRemove}", ex);
                this.dialogService.ShowMessage("An error occurred while removing the rule. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task RemoveAllRules()
        {
            if (this.AllRules.Count == 0)
            {
                this.dialogService.ShowMessage("No rules found to remove.", "No Rules");
                return;
            }

            var confirmMessage = $"This will remove ALL {this.AllRules.Count} AppIntBlocker firewall rules.\n\n" +
                               "This action cannot be undone. Are you sure you want to continue?";

            if (!this.dialogService.ShowConfirmation(confirmMessage, "Confirm Remove All Rules"))
            {
                this.loggingService.LogInfo("Remove all rules operation cancelled by user");
                return;
            }

            try
            {
                this.loggingService.LogInfo("Starting removal of all AppIntBlocker rules...");

                // Get unique application names
                var applicationNames = this.AllRules.Select(r => r.ApplicationName).Distinct().ToList();

                // Run removal operations in background
                var(successCount, errorCount) = await Task.Run(async () =>
                {
                    int success = 0;
                    int errors = 0;

                    foreach (var appName in applicationNames)
                    {
                        try
                        {
                            var result = await this.firewallService.RemoveExistingRules(appName, this.loggingService);
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
                            this.loggingService.LogError($"Error removing rules for {appName}", ex);
                        }
                    }

                    return (success, errors);
                });

                this.loggingService.LogInfo($"Remove all operation completed. Success: {successCount}, Errors: {errorCount}");

                if (errorCount > 0)
                {
                    this.dialogService.ShowMessage($"Completed with {errorCount} errors. Check the log for details.", "Partial Success");
                }
                else
                {
                    this.dialogService.ShowMessage("Successfully removed all AppIntBlocker rules.", "Success");
                }

                await this.RefreshRulesAsync();
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error during remove all rules operation", ex);
                this.dialogService.ShowMessage("An error occurred while removing rules. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task ExportRules()
        {
            try
            {
                if (this.AllRules.Count == 0)
                {
                    this.dialogService.ShowMessage("No rules to export.", "No Data");
                    return;
                }

                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var fileName = $"AppIntBlocker_Rules_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(documentsPath, fileName);

                var exportContent = new List<string>
                {
                    "AppIntBlocker Firewall Rules Export",
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Total Rules: {this.AllRules.Count}",
                    string.Empty,
                    "Rules Details:",
                    string.Empty.PadRight(80, '=')
                };

                foreach (var rule in this.AllRules.OrderBy(r => r.ApplicationName).ThenBy(r => r.RuleName))
                {
                    exportContent.Add($"Rule Name: {rule.RuleName}");
                    exportContent.Add($"Application: {rule.ApplicationName}");
                    exportContent.Add($"Direction: {rule.Direction}");
                    exportContent.Add($"Status: {rule.Status}");
                    exportContent.Add($"Program: {rule.ProgramPath}");
                    exportContent.Add(string.Empty);
                }

                await File.WriteAllLinesAsync(filePath, exportContent);

                this.loggingService.LogInfo($"Rules exported to: {filePath}");
                this.dialogService.ShowMessage($"Rules successfully exported to:\n{filePath}", "Export Complete");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error exporting rules", ex);
                this.dialogService.ShowMessage("Failed to export rules. Check the log for details.", "Export Error");
            }
        }

        [RelayCommand]
        private void ImportRules()
        {
            try
            {
                this.loggingService.LogInfo("Opening import file dialog...");

                // FIXED: Use dialog service
                var fileName = this.dialogService.OpenFileDialog(
                    "Import Firewall Rules",
                    "XML Files (*.xml)|*.xml|Text Files (*.txt)|*.txt|All Files (*.*)|*.*");

                if (!string.IsNullOrEmpty(fileName))
                {
                    this.loggingService.LogInfo($"Importing rules from: {fileName}");

                    // For now, just log the import action
                    // Future implementation would parse and import the rules
                    this.dialogService.ShowMessage("Import feature coming soon!", "Import Rules");

                    this.loggingService.LogInfo("Import operation completed");
                }
                else
                {
                    this.loggingService.LogInfo("Import operation cancelled by user");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error importing rules", ex);
                this.dialogService.ShowError("Failed to import rules. Check the log for details.");
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            this.OperationLog = string.Empty;
            this.loggingService.LogInfo("Operation log cleared by user");
        }

        public void Dispose()
        {
            this.loggingService.LogEntryAdded -= this.OnLogEntryAdded;
        }
    }
}
