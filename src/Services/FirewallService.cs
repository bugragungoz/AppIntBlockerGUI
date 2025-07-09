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
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Core;
    using Microsoft.Extensions.ObjectPool;

    [SupportedOSPlatform("windows")]
    public class FirewallService : IFirewallService
    {
        private const string RuleNamePrefix = "AppBlocker Rule - ";
        private readonly ILoggingService loggingService;
        private readonly Func<IPowerShellWrapper> powerShellWrapperFactory;

        public FirewallService(ILoggingService loggingService, Func<IPowerShellWrapper> powerShellWrapperFactory)
        {
            this.loggingService = loggingService;
            this.powerShellWrapperFactory = powerShellWrapperFactory;
        }

        public async Task<bool> BlockApplicationFiles(
            string path,
            bool blockExe,
            bool blockDll,
            bool includeSubdirectories,
            List<string> excludedKeywords,
            List<string> excludedFiles,
            ILoggingService logger, // This logger is redundant but kept for now to avoid breaking changes in the call signature if it's public. Let's assume it's part of a public interface for now.
            CancellationToken cancellationToken)
        {
            using (var powerShell = this.powerShellWrapperFactory())
            {
                try
                {
                    if (!await this.ImportFirewallModules(powerShell, cancellationToken).ConfigureAwait(false))
                    {
                        this.loggingService.LogError("Failed to import required PowerShell modules. Aborting block operation.");
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
                        this.loggingService.LogWarning("No file types selected to block (.exe or .dll).");
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
                        this.loggingService.LogWarning($"No files found to block in {path}");
                        return false;
                    }

                    var filteredFiles = filesToBlock
                        .Where(file => !excludedFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                        .Where(file => !excludedKeywords.Any(keyword => Path.GetFileName(file).Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    this.loggingService.LogInfo($"Found {filteredFiles.Count} files to block after applying exclusions.");

                    foreach (var file in filteredFiles)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            this.loggingService.LogWarning("Block operation cancelled by user.");
                            cancellationToken.ThrowIfCancellationRequested(); // Throw standard exception
                        }

                        var ruleName = $"AppBlocker Rule - {applicationName} - {Path.GetFileName(file)}";
                        await this.CreateFirewallRule(powerShell, file, ruleName, "Inbound", cancellationToken).ConfigureAwait(false);
                        await this.CreateFirewallRule(powerShell, file, ruleName, "Outbound", cancellationToken).ConfigureAwait(false);
                    }

                    this.loggingService.LogInfo($"Block operation completed for {applicationName}.");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    this.loggingService.LogWarning("Block operation was cancelled.");
                    return false;
                }
                catch (Exception ex)
                {
                    this.loggingService.LogError("An unexpected error occurred during the block operation.", ex);
                    return false;
                }
            }
        }

        private async Task<bool> ImportFirewallModules(IPowerShellWrapper powerShell, CancellationToken cancellationToken = default)
        {
            try
            {
                powerShell.AddScript("Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force");
                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.ToString()).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    this.loggingService.LogWarning($"Warning setting execution policy: {string.Join("; ", errors)}");
                }

                powerShell.AddScript("Import-Module NetSecurity -Force");
                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.ToString()).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    this.loggingService.LogError($"CRITICAL: NetSecurity module import failed. Firewall operations cannot proceed: {string.Join("; ", errors)}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("CRITICAL: Error importing PowerShell modules. Firewall operations cannot proceed.", ex);
                return false;
            }
        }

        private async Task<bool> CreateFirewallRule(IPowerShellWrapper powerShell, string filePath, string displayName, string direction, CancellationToken cancellationToken = default)
        {
            try
            {
                if (await this.TryCreateFirewallRuleWithPowerShell(powerShell, filePath, displayName, direction, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                this.loggingService.LogInfo($"PowerShell failed, trying netsh for rule: {displayName}");
                return await this.CreateFirewallRuleWithNetsh(filePath, displayName, direction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Exception creating firewall rule '{displayName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryCreateFirewallRuleWithPowerShell(IPowerShellWrapper powerShell, string filePath, string displayName, string direction, CancellationToken cancellationToken = default)
        {
            try
            {
                var script = $"New-NetFirewallRule -DisplayName '{displayName.Replace("'", "''")}' -Direction {direction} -Program '{filePath.Replace("'", "''")}' -Action Block -ErrorAction Stop";
                powerShell.AddScript(script);

                var results = await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errorMessages = powerShell.Errors.Select(e => e.ToString()).ToList();
                    var fullError = string.Join("; ", errorMessages);

                    if (fullError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                    {
                        this.loggingService.LogInfo($"Rule '{displayName}' already exists, considering it a success.");
                        return true;
                    }
                    
                    this.loggingService.LogError($"PowerShell errors creating rule '{displayName}': {fullError}");
                    return false;
                }

                this.loggingService.LogInfo($"Successfully created firewall rule with PowerShell: {displayName}");
                return true;
            }
            catch (Exception ex)
            {
                this.loggingService.LogWarning($"PowerShell method failed for '{displayName}': {ex.Message}");
                // Returning false to allow fallback to netsh
                return false;
            }
        }

        private async Task<bool> CreateFirewallRuleWithNetsh(string filePath, string displayName, string direction, CancellationToken cancellationToken = default)
        {
            var processInfo = new ProcessStartInfo("netsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Use ArgumentList for safer argument handling
            processInfo.ArgumentList.Add("advfirewall");
            processInfo.ArgumentList.Add("firewall");
            processInfo.ArgumentList.Add("add");
            processInfo.ArgumentList.Add("rule");
            processInfo.ArgumentList.Add($"name={EscapeNetshArgument(displayName)}");
            processInfo.ArgumentList.Add($"dir={direction.ToLower()}");
            processInfo.ArgumentList.Add("action=block");
            processInfo.ArgumentList.Add($"program={EscapeNetshArgument(filePath)}");
            processInfo.ArgumentList.Add("enable=yes");

            try
            {
                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        this.loggingService.LogError($"Failed to start netsh process for rule: {displayName}");
                        return false;
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                    var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (process.ExitCode == 0)
                    {
                        this.loggingService.LogInfo($"Successfully created firewall rule with netsh: {displayName}");
                        return true;
                    }
                    else
                    {
                        var error = await errorTask;
                        if (error.Contains("Ok.") || error.Contains("Rule already exists"))
                        {
                            this.loggingService.LogInfo($"netsh reported rule '{displayName}' already exists or was created successfully.");
                            return true;
                        }
                        throw new InvalidOperationException($"netsh exit code: {process.ExitCode}, Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"netsh method also failed for '{displayName}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveExistingRules(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            var ruleNamePattern = $"{RuleNamePrefix}{applicationName.Replace("'", "''")}*";
            logger.LogInfo($"Attempting to remove firewall rules matching pattern: {ruleNamePattern}");

            // PowerShell is generally more reliable for removal and querying
            if (await this.TryRemoveRulesWithPowerShell(ruleNamePattern, logger, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInfo("Successfully removed rules with PowerShell.");
                return true;
            }

            // Fallback to netsh if PowerShell fails
            logger.LogWarning("PowerShell removal failed. Falling back to netsh.");
            if (await this.RemoveRulesWithNetsh(applicationName, logger, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInfo("Successfully removed rules with netsh as a fallback.");
                return true;
            }

            logger.LogError($"Failed to remove rules for '{applicationName}' with both PowerShell and netsh.");
            return false;
        }

        private async Task<bool> TryRemoveRulesWithPowerShell(string ruleNamePattern, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            using (var powerShell = this.powerShellWrapperFactory())
            {
                try
                {
                    var script = $"Remove-NetFirewallRule -DisplayName '{ruleNamePattern}' -ErrorAction Stop";
                    powerShell.AddScript(script);
                    await powerShell.InvokeAsync();

                    if (powerShell.HadErrors)
                    {
                        var errorMessages = powerShell.Errors.Select(e => e.ToString()).ToList();
                        var fullError = string.Join("; ", errorMessages);
                        // If no rules were found, it's not a critical error.
                        if (fullError.Contains("No matching MSFT_NetFirewallRule found", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInfo($"No rules found matching '{ruleNamePattern}', considering removal successful.");
                            return true;
                        }

                        throw new Exception($"PowerShell errors: {fullError}");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error removing firewall rules with PowerShell: {ex.Message}", ex);
                    return false;
                }
            }
        }

        private Task<bool> RemoveRulesWithNetsh(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            // netsh does not support wildcard deletion easily. This is a best-effort fallback.
            // It's generally better to rely on the PowerShell implementation.
            logger.LogWarning("netsh fallback for rule removal is not implemented due to complexity and unreliability of wildcard deletion.");
            return Task.FromResult(false);
        }

        public async Task<List<string>> GetExistingRulesAsync(CancellationToken cancellationToken = default)
        {
            using (var powerShell = this.powerShellWrapperFactory())
            {
                var ruleNames = new List<string>();
                try
                {
                    powerShell.AddScript($"Get-NetFirewallRule -DisplayName '{RuleNamePrefix}*' | Select-Object DisplayName");

                    var results = await powerShell.InvokeAsync();

                    if (powerShell.HadErrors)
                    {
                        var errors = powerShell.Errors.Select(e => e.ToString()).ToList();
                        this.loggingService.LogError($"Error getting existing rules: {string.Join("; ", errors)}");
                        return ruleNames;
                    }

                    foreach (var result in results)
                    {
                        if (result?.BaseObject is PSObject psObject && psObject.Properties["DisplayName"]?.Value is string displayName)
                        {
                            ruleNames.Add(displayName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.loggingService.LogError("Failed to get existing firewall rules.", ex);
                }

                return ruleNames;
            }
        }

        private async Task<bool> RunPowerShellScript(string script, ILoggingService loggingService, CancellationToken cancellationToken)
        {
            using (var ps = this.powerShellWrapperFactory())
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

        public async Task<bool> RemoveSingleRule(string ruleName, CancellationToken cancellationToken = default)
        {
            this.loggingService.LogInfo($"Attempting to remove single rule: {ruleName}");

            if (await this.TryRemoveSingleRuleWithPowerShell(ruleName, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            this.loggingService.LogWarning("PowerShell failed for single rule removal, trying netsh.");
            return await this.RemoveSingleRuleWithNetsh(ruleName, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> TryRemoveSingleRuleWithPowerShell(string ruleName, CancellationToken cancellationToken = default)
        {
            using (var powerShell = this.powerShellWrapperFactory())
            {
                try
                {
                    var script = $"Remove-NetFirewallRule -DisplayName '{ruleName.Replace("'", "''")}' -ErrorAction Stop";
                    powerShell.AddScript(script);
                    await powerShell.InvokeAsync();

                    if (powerShell.HadErrors)
                    {
                        var errors = powerShell.Errors.Select(e => e.ToString()).ToList();
                        this.loggingService.LogError($"Error removing single rule '{ruleName}' with PowerShell: {string.Join("; ", errors)}");
                        return false;
                    }

                    this.loggingService.LogInfo($"Successfully removed rule '{ruleName}' with PowerShell.");
                    return true;
                }
                catch (Exception ex)
                {
                    this.loggingService.LogError($"Exception removing single rule '{ruleName}' with PowerShell.", ex);
                    return false;
                }
            }
        }

        private async Task<bool> RemoveSingleRuleWithNetsh(string ruleName, CancellationToken cancellationToken = default)
        {
            var processInfo = new ProcessStartInfo("netsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            processInfo.ArgumentList.Add("advfirewall");
            processInfo.ArgumentList.Add("firewall");
            processInfo.ArgumentList.Add("delete");
            processInfo.ArgumentList.Add("rule");
            processInfo.ArgumentList.Add($"name={EscapeNetshArgument(ruleName)}");
            
            try
            {
                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        this.loggingService.LogError($"Failed to start netsh process for rule deletion: {ruleName}");
                        return false;
                    }

                    var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                    await process.WaitForExitAsync(cancellationToken);

                    if (process.ExitCode != 0)
                    {
                        if (error.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
                        {
                            this.loggingService.LogWarning($"netsh: Rule '{ruleName}' not found for deletion.");
                            return true; // Consider it a success if the rule doesn't exist
                        }
                        throw new InvalidOperationException($"netsh exit code: {process.ExitCode}, Error: {error}");
                    }
                    
                    this.loggingService.LogInfo($"Successfully deleted rule '{ruleName}' with netsh.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"netsh method for single rule deletion failed for '{ruleName}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateSystemRestorePoint(string description, CancellationToken cancellationToken = default)
        {
            this.loggingService.LogInfo("Attempting to create a system restore point...");
            using (var powerShell = this.powerShellWrapperFactory())
            {
                try
                {
                    if (!await this.ImportFirewallModules(powerShell, cancellationToken).ConfigureAwait(false)) return false;

                    powerShell.AddScript($"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS'");
                    var results = await powerShell.InvokeAsync();

                    if (powerShell.HadErrors)
                    {
                        var errors = powerShell.Errors.Select(e => e.ToString()).ToList();
                        this.loggingService.LogError($"Failed to create system restore point: {string.Join("; ", errors)}");
                        return false;
                    }

                    this.loggingService.LogInfo("System restore point created successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    this.loggingService.LogError("An exception occurred while creating the system restore point.", ex);
                    return false;
                }
            }
        }

        public async Task<List<AppIntBlockerGUI.Models.FirewallRuleModel>> GetAllFirewallRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = new List<AppIntBlockerGUI.Models.FirewallRuleModel>();
            using (var powerShell = this.powerShellWrapperFactory())
            {
                try
                {
                    if (!await this.ImportFirewallModules(powerShell, cancellationToken).ConfigureAwait(false)) return rules;

                    powerShell.AddScript($"Get-NetFirewallRule -DisplayName '{RuleNamePrefix}*' | Select-Object DisplayName, Enabled, Direction, Action");

                    var results = await powerShell.InvokeAsync();

                    if (powerShell.HadErrors)
                    {
                        var errors = powerShell.Errors.Select(e => e.ToString()).ToList();
                        this.loggingService.LogError($"Error getting all firewall rules: {string.Join("; ", errors)}");
                        return rules; // Return empty list on error
                    }

                    if (results != null)
                    {
                        foreach (var psObject in results)
                        {
                            if (psObject?.Properties["DisplayName"]?.Value is string displayName)
                            {
                                var rule = new Models.FirewallRuleModel
                                {
                                    DisplayName = displayName,
                                    RuleName = psObject.Properties["Name"]?.Value as string ?? string.Empty,
                                    IsEnabled = psObject.Properties["Enabled"]?.Value as bool? ?? false,
                                    Enabled = psObject.Properties["Enabled"]?.Value as bool? ?? false,
                                    Direction = psObject.Properties["Direction"]?.Value as string ?? "Unknown",
                                    Action = psObject.Properties["Action"]?.Value as string ?? "Unknown",
                                    ProgramPath = psObject.Properties["Program"]?.Value as string ?? "N/A",
                                    IsAppIntBlockerRule = displayName.StartsWith(RuleNamePrefix, StringComparison.OrdinalIgnoreCase)
                                };
                                rules.Add(rule);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.loggingService.LogError("Exception in GetAllFirewallRulesAsync", ex);
                }

                return rules;
            }
        }

        public async Task<bool> DeleteRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var script = $"Remove-NetFirewallRule -DisplayName '{ruleName.Replace("'", "''")}'";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    this.loggingService.LogError($"Error deleting rule '{ruleName}': {string.Join("; ", errors)}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Exception deleting rule '{ruleName}'", ex);
                return false;
            }
        }

        public async Task<bool> ToggleRuleAsync(string ruleName, bool enable, CancellationToken cancellationToken = default)
        {
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                var enabledState = enable ? "True" : "False";
                var script = $"Set-NetFirewallRule -DisplayName '{ruleName.Replace("'", "''")}' -Enabled {enabledState}";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    this.loggingService.LogError($"Error toggling rule '{ruleName}': {string.Join("; ", errors)}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Exception toggling rule '{ruleName}'", ex);
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
                this.loggingService.LogError("Failed to open Windows Firewall with Advanced Security.", ex);
            }
        }

        private static string EscapeNetshArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}
