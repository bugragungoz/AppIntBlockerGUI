// <copyright file="FirewallService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Services;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.ObjectPool;
    using System.Runtime.Versioning;
    using AppIntBlockerGUI.Core;

    [SupportedOSPlatform("windows")]
    public class FirewallService : IFirewallService
    {
        private const string RuleNamePrefix = "AppBlocker Rule - ";
        private readonly ILoggingService _loggingService;
        private readonly Func<IPowerShellWrapper> _powerShellWrapperFactory;

        public FirewallService(ILoggingService loggingService, Func<IPowerShellWrapper> powerShellWrapperFactory)
        {
            _loggingService = loggingService;
            _powerShellWrapperFactory = powerShellWrapperFactory;
        }

        public async Task<bool> BlockApplicationFiles(
            string path,
            bool blockExe,
            bool blockDll,
            bool includeSubdirectories,
            List<string> excludedKeywords,
            List<string> excludedFiles,
            ILoggingService logger,
            CancellationToken cancellationToken)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogError("Failed to import required PowerShell modules. Aborting block operation.");
                    return false;
                }

                var extensions = new List<string>();
                if (blockExe)
                {
                    extensions.Add("*.exe");
                }

                if (blockDll)
                {
                    extensions.Add("*.dll");
                }

                if (!extensions.Any())
                {
                    logger.LogWarning("No file types selected to block (.exe or .dll).");
                    return false;
                }

                var applicationName = new DirectoryInfo(path).Name;

                var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var filesToBlock = new List<string>();

                foreach (var ext in extensions)
                {
                    filesToBlock.AddRange(Directory.GetFiles(path, ext, searchOption));
                }

                if (!filesToBlock.Any())
                {
                    logger.LogWarning($"No files found to block in {path}");
                    return false;
                }

                var filteredFiles = filesToBlock
                    .Where(file => !excludedFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                    .Where(file => !excludedKeywords.Any(keyword => Path.GetFileName(file).Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                logger.LogInfo($"Found {filteredFiles.Count} files to block after applying exclusions.");

                foreach (var file in filteredFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Block operation cancelled by user.");
                        return false;
                    }

                    var ruleName = $"AppBlocker Rule - {applicationName} - {Path.GetFileName(file)}";
                    await this.CreateFirewallRule(powerShell, file, ruleName, "Inbound", logger, cancellationToken).ConfigureAwait(false);
                    await this.CreateFirewallRule(powerShell, file, ruleName, "Outbound", logger, cancellationToken).ConfigureAwait(false);
                }

                logger.LogInfo($"Block operation completed for {applicationName}.");
                return true;
            }
            finally
            {
                // No need to return the powerShell object as it's managed by the factory
            }
        }

        private async Task<bool> ImportFirewallModules(IPowerShellWrapper powerShell, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                // Set execution policy to bypass for this session
                powerShell.AddScript("Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force");
                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    logger.LogWarning($"Warning setting execution policy: {string.Join("; ", errors)}");
                }

                powerShell.Errors.Clear();

                // Import NetSecurity module for firewall cmdlets
                powerShell.AddScript("Import-Module NetSecurity -Force");
                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    logger.LogError($"CRITICAL: NetSecurity module import failed. Firewall operations cannot proceed: {string.Join("; ", errors)}");
                    return false;  // Signal failure clearly
                }

                powerShell.Errors.Clear();
                return true;  // Success
            }
            catch (Exception ex)
            {
                logger.LogError("CRITICAL: Error importing PowerShell modules. Firewall operations cannot proceed.", ex);
                return false;  // Signal failure clearly
            }
        }

        private async Task<bool> CreateFirewallRule(IPowerShellWrapper powerShell, string filePath, string displayName, string direction, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                // Try PowerShell first
                if (await this.TryCreateFirewallRuleWithPowerShell(powerShell, filePath, displayName, direction, logger, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                // Fallback to netsh command
                logger.LogInfo($"PowerShell failed, trying netsh for rule: {displayName}");
                return await this.CreateFirewallRuleWithNetsh(filePath, displayName, direction, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception creating firewall rule '{displayName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryCreateFirewallRuleWithPowerShell(IPowerShellWrapper powerShell, string filePath, string displayName, string direction, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                powerShell.AddCommand("New-NetFirewallRule")
                    .AddParameter("DisplayName", displayName)
                    .AddParameter("Direction", direction)
                    .AddParameter("Program", filePath)
                    .AddParameter("Action", "Block")
                    .AddParameter("ErrorAction", "Stop");

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    throw new Exception($"PowerShell errors: {errorMessage}");
                }

                logger.LogInfo($"Successfully created firewall rule with PowerShell: {displayName}");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"PowerShell method failed for '{displayName}': {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CreateFirewallRuleWithNetsh(string filePath, string displayName, string direction, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name={EscapeNetshArgument(displayName)} dir={direction.ToLower()} action=block program={EscapeNetshArgument(filePath)} enable=yes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    // CRITICAL FIX: Check for null
                    if (process == null)
                    {
                        logger.LogError($"Failed to start netsh process for rule: {displayName}");
                        return false;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (process.ExitCode == 0)
                    {
                        logger.LogInfo($"Successfully created firewall rule with netsh: {displayName}");
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"netsh exit code: {process.ExitCode}, Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"netsh method also failed for '{displayName}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveExistingRules(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInfo($"Removing firewall rules for application: {applicationName}");

                // Try PowerShell first
                if (await this.TryRemoveRulesWithPowerShell(applicationName, logger, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                // Fallback to netsh
                logger.LogInfo("PowerShell removal failed, trying netsh approach...");
                return await this.RemoveRulesWithNetsh(applicationName, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error removing rules for '{applicationName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryRemoveRulesWithPowerShell(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                // Import required modules
                await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false);

                // First, get ALL rules and filter manually (wildcard doesn't work reliably)
                powerShell.AddCommand("Get-NetFirewallRule")
                    .AddParameter("ErrorAction", "SilentlyContinue");

                var allRules = await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    logger.LogWarning($"PowerShell errors getting rules: {string.Join("; ", errors)}");
                    return false;
                }

                // Filter rules manually by DisplayName
                var targetPrefix = $"{RuleNamePrefix}{applicationName}";
                var matchingRules = allRules.Where(rule =>
                {
                    var displayName = rule.Properties["DisplayName"]?.Value?.ToString();
                    return !string.IsNullOrEmpty(displayName) && displayName.StartsWith(targetPrefix);
                }).ToList();

                logger.LogInfo($"Found {allRules.Count} total firewall rules, {matchingRules.Count} matching '{targetPrefix}'");

                if (!matchingRules.Any())
                {
                    logger.LogInfo($"No existing rules found for application '{applicationName}'.");
                    return false;
                }

                // Remove each rule individually using DisplayName
                int removedCount = 0;
                foreach (var rule in matchingRules)
                {
                    try
                    {
                        var displayName = rule.Properties["DisplayName"]?.Value?.ToString();

                        if (!string.IsNullOrEmpty(displayName))
                        {
                            powerShell.AddCommand("Remove-NetFirewallRule")
                                .AddParameter("DisplayName", displayName)
                                .AddParameter("ErrorAction", "Stop");

                            await powerShell.InvokeAsync();

                            if (!powerShell.HadErrors)
                            {
                                removedCount++;
                                logger.LogInfo($"Successfully removed rule with PowerShell: {displayName}");
                            }
                            else
                            {
                                var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                                logger.LogWarning($"PowerShell error removing rule '{displayName}': {string.Join("; ", errors)}");
                            }

                            powerShell.Errors.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Exception removing individual rule: {ex.Message}");
                    }
                }

                logger.LogInfo($"Successfully removed {removedCount} out of {matchingRules.Count} rule(s) for '{applicationName}'.");
                return removedCount > 0;
            }
            finally
            {
                // No need to return the powerShell object as it's managed by the factory
            }
        }

        private async Task<bool> RemoveRulesWithNetsh(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get list of rules first using netsh
                var listProcess = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=all",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var targetPrefix = $"{RuleNamePrefix}{applicationName}";
                var rulesToRemove = new List<string>();

                using (var process = Process.Start(listProcess))
                {
                    if (process == null)
                    {
                        logger.LogError("Failed to start netsh process for listing rules");
                        return false;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    // Parse output to find matching rule names
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Rule Name:") && line.Contains(targetPrefix))
                        {
                            var ruleName = line.Substring("Rule Name:".Length).Trim();
                            rulesToRemove.Add(ruleName);
                        }
                    }
                }

                logger.LogInfo($"Found {rulesToRemove.Count} rules to remove with netsh");

                if (!rulesToRemove.Any())
                {
                    logger.LogInfo($"No rules found for application '{applicationName}' using netsh");
                    return false;
                }

                // Remove each rule
                int removedCount = 0;
                foreach (var ruleName in rulesToRemove)
                {
                    try
                    {
                        var removeProcess = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = $"advfirewall firewall delete rule name={EscapeNetshArgument(ruleName)}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(removeProcess))
                        {
                            if (process == null)
                            {
                                logger.LogError($"Failed to start netsh process to remove rule: {ruleName}");
                                continue;
                            }

                            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                            if (process.ExitCode == 0)
                            {
                                removedCount++;
                                logger.LogInfo($"Successfully removed rule with netsh: {ruleName}");
                            }
                            else
                            {
                                logger.LogWarning($"Failed to remove rule '{ruleName}' with netsh. Error: {error}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Exception removing rule '{ruleName}' with netsh: {ex.Message}");
                    }
                }

                logger.LogInfo($"Successfully removed {removedCount} out of {rulesToRemove.Count} rule(s) for '{applicationName}' using netsh.");
                return removedCount > 0;
            }
            catch (Exception ex)
            {
                logger.LogError($"netsh removal method failed: {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetExistingRulesAsync(ILoggingService loggingService, CancellationToken cancellationToken = default)
        {
            var ruleNames = new List<string>();
            var script = "Get-NetFirewallRule -DisplayName \"AppBlocker Rule - *\" | Select-Object DisplayName";

            using (var ps = _powerShellWrapperFactory())
            {
                ps.AddScript(script);

                loggingService.LogInfo("Executing PowerShell to get existing firewall rules...");

                var results = await ps.InvokeAsync();

                if (ps.HadErrors)
                {
                    foreach (var error in ps.Errors)
                    {
                        loggingService.LogError($"PowerShell error: {error}", error.Exception);
                    }
                }

                foreach (var result in results)
                {
                    if (result != null && result.Properties["DisplayName"] != null)
                    {
                        ruleNames.Add(result.Properties["DisplayName"].Value.ToString());
                    }
                }
            }

            loggingService.LogInfo($"Found {ruleNames.Count} existing AppIntBlocker rules.");
            return ruleNames;
        }

        private async Task<bool> RunPowerShellScript(string script, ILoggingService loggingService, CancellationToken cancellationToken)
        {
            using (var ps = _powerShellWrapperFactory())
            {
                ps.AddScript(script);

                loggingService.LogInfo($"Executing PowerShell script... Script: {script.Substring(0, Math.Min(script.Length, 100))}");

                await ps.InvokeAsync();

                if (ps.HadErrors)
                {
                    foreach (var error in ps.Errors)
                    {
                        loggingService.LogError($"PowerShell script error: {error}", error.Exception);
                    }

                    return false;
                }
            }

            return true;
        }

        public async Task<bool> RemoveSingleRule(string ruleName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInfo($"Removing single firewall rule: {ruleName}");

                // First try PowerShell
                if (await this.TryRemoveSingleRuleWithPowerShell(ruleName, logger, cancellationToken))
                {
                    return true;
                }

                // Fallback to netsh
                logger.LogInfo("PowerShell failed, trying netsh for single rule removal");
                return await this.RemoveSingleRuleWithNetsh(ruleName, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error removing single rule '{ruleName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryRemoveSingleRuleWithPowerShell(string ruleName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false);

                powerShell.AddCommand("Remove-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    logger.LogWarning($"PowerShell error removing rule '{ruleName}': {errorMessage}");
                    return false;
                }

                logger.LogInfo($"Successfully removed rule with PowerShell: {ruleName}");
                return true;
            }
            finally
            {
                // No need to return the powerShell object as it's managed by the factory
            }
        }

        private async Task<bool> RemoveSingleRuleWithNetsh(string ruleName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall delete rule name={EscapeNetshArgument(ruleName)}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        logger.LogError($"Failed to start netsh process to remove single rule: {ruleName}");
                        return false;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (process.ExitCode == 0)
                    {
                        logger.LogInfo($"Successfully removed rule with netsh: {ruleName}");
                        return true;
                    }
                    else
                    {
                        logger.LogWarning($"Failed to remove rule '{ruleName}' with netsh. Error: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"netsh single rule removal failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateSystemRestorePoint(string description, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                logger.LogInfo($"Creating system restore point: {description}");

                powerShell.AddCommand("Checkpoint-Computer")
                    .AddParameter("Description", description)
                    .AddParameter("RestorePointType", "MODIFY_SETTINGS")
                    .AddParameter("ErrorAction", "Stop");

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    logger.LogError($"PowerShell error creating restore point: {errorMessage}");
                    return false;
                }

                logger.LogInfo("System restore point created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("Error creating system restore point", ex);
                return false;
            }
        }

        public async Task<List<AppIntBlockerGUI.Models.FirewallRuleModel>> GetAllFirewallRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = new List<AppIntBlockerGUI.Models.FirewallRuleModel>();
            var powerShell = _powerShellWrapperFactory();

            try
            {
                // Get all firewall rules
                powerShell.AddCommand("Get-NetFirewallRule")
                    .AddParameter("ErrorAction", "SilentlyContinue");

                var psResults = await powerShell.InvokeAsync();

                foreach (var psObject in psResults)
                {
                    var rule = new AppIntBlockerGUI.Models.FirewallRuleModel
                    {
                        DisplayName = psObject.Properties["DisplayName"]?.Value?.ToString() ?? "Unknown",
                        RuleName = psObject.Properties["Name"]?.Value?.ToString() ?? string.Empty,
                        Direction = psObject.Properties["Direction"]?.Value?.ToString() ?? string.Empty,
                        Action = psObject.Properties["Action"]?.Value?.ToString() ?? string.Empty,
                        Protocol = psObject.Properties["Protocol"]?.Value?.ToString() ?? string.Empty,
                        Profile = psObject.Properties["Profile"]?.Value?.ToString() ?? string.Empty,
                        Description = psObject.Properties["Description"]?.Value?.ToString() ?? string.Empty,
                        Group = psObject.Properties["Group"]?.Value?.ToString() ?? string.Empty,
                    };

                    if (bool.TryParse(psObject.Properties["Enabled"]?.Value?.ToString(), out bool enabled))
                    {
                        rule.Enabled = enabled;
                        rule.IsEnabled = enabled;
                    }

                    rule.Status = rule.Enabled ? "Enabled" : "Disabled";
                    rule.IsAppIntBlockerRule = rule.DisplayName.StartsWith("AppBlocker Rule", StringComparison.OrdinalIgnoreCase);

                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                // FIXED: Log the exception instead of silent fail
                System.Diagnostics.Debug.WriteLine($"Exception getting all firewall rules: {ex.Message}");

                // Return empty list on error
            }

            return rules;
        }

        public async Task<bool> DeleteRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                powerShell.AddCommand("Remove-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    System.Diagnostics.Debug.WriteLine($"PowerShell errors deleting rule '{ruleName}': {errorMessage}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // FIXED: Log the exception instead of silent fail
                System.Diagnostics.Debug.WriteLine($"Exception deleting rule '{ruleName}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ToggleRuleAsync(string ruleName, bool enable, CancellationToken cancellationToken = default)
        {
            var powerShell = _powerShellWrapperFactory();
            try
            {
                var action = enable ? "Enable" : "Disable";
                powerShell.AddCommand($"{action}-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    System.Diagnostics.Debug.WriteLine($"PowerShell errors toggling rule '{ruleName}' to {(enable ? "enabled" : "disabled")}: {errorMessage}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // FIXED: Log the exception instead of silent fail
                System.Diagnostics.Debug.WriteLine($"Exception toggling rule '{ruleName}' to {(enable ? "enabled" : "disabled")}: {ex.Message}");
                return false;
            }
        }

        public void OpenWindowsFirewallWithAdvancedSecurity()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "wf.msc",
                    UseShellExecute = true
                });

                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Windows Firewall management console");
                }
            }
            catch (Exception ex)
            {
                // FIXED: Log the exception instead of silent fail
                // Note: We don't have logger here, but we should avoid silent failures
                System.Diagnostics.Debug.WriteLine($"Failed to open Windows Firewall with Advanced Security: {ex.Message}");

                // Consider showing user-friendly message in a real application
                // _dialogService?.ShowError("Could not open Windows Firewall management console. " +
                //     "Please open it manually from Control Panel.");
            }
        }

        private static string EscapeNetshArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            // Per netsh documentation, a quote is escaped by doubling it.
            return "\"" + argument.Replace("\"", "\"\"") + "\"";
        }
    }
}
