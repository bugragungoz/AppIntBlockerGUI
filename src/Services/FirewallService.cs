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
            ILoggingService logger,
            CancellationToken cancellationToken)
        {
            var powerShell = this.powerShellWrapperFactory();
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

                // Import NetSecurity module for firewall cmdlets
                powerShell.AddScript("Import-Module NetSecurity -Force");
                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    logger.LogError($"CRITICAL: NetSecurity module import failed. Firewall operations cannot proceed: {string.Join("; ", errors)}");
                    return false;  // Signal failure clearly
                }

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
                var script = $"New-NetFirewallRule -DisplayName '{displayName.Replace("'", "''")}' -Direction {direction} -Program '{filePath.Replace("'", "''")}' -Action Block -ErrorAction Stop";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);

                    if (errorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"Rule '{displayName}' already exists, considering it a success.");
                        return true;
                    }

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
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false))
                {
                    return false; // Error already logged
                }

                var rulePattern = $"{RuleNamePrefix}{applicationName.Replace("'", "''")}*";
                var script = $"Get-NetFirewallRule -DisplayName '{rulePattern}' | Remove-NetFirewallRule";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"No rules found for '{applicationName}', considering it a success.");
                        return true;
                    }

                    throw new Exception($"PowerShell errors: {errorMessage}");
                }

                logger.LogInfo($"Successfully removed rules with PowerShell for: {applicationName}");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"PowerShell method failed for removing rules for '{applicationName}': {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RemoveRulesWithNetsh(string applicationName, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            var rulePattern = $"{RuleNamePrefix}{applicationName}*";
            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name={EscapeNetshArgument(rulePattern)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        logger.LogError($"Failed to start netsh process for removing rules for: {applicationName}");
                        return false;
                    }

                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    if (process.ExitCode == 0)
                    {
                        logger.LogInfo($"Successfully removed rules with netsh for: {applicationName}");
                        return true;
                    }
                    else if (error.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"No rules to remove with netsh for '{applicationName}', considering it a success.");
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"netsh exit code: {process.ExitCode}, Output: {output}, Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"netsh method also failed for removing rules for '{applicationName}': {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetExistingRulesAsync(ILoggingService loggingService, CancellationToken cancellationToken = default)
        {
            var existingRules = new List<string>();
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, loggingService, cancellationToken).ConfigureAwait(false))
                {
                    return existingRules; // Error already logged
                }

                var script = $"Get-NetFirewallRule -DisplayName '{RuleNamePrefix}*' | Select-Object -ExpandProperty DisplayName";
                powerShell.AddScript(script);

                var results = await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    loggingService.LogError($"Error getting existing rules: {string.Join("; ", errors)}");
                }

                if (results != null)
                {
                    foreach (var psObject in results)
                    {
                        if (psObject != null && psObject.BaseObject is string ruleName)
                        {
                            existingRules.Add(ruleName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                loggingService.LogError("Exception in GetExistingRulesAsync", ex);
            }

            return existingRules;
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
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false))
                {
                    return false; // Error already logged
                }

                var script = $"Remove-NetFirewallRule -DisplayName '{ruleName.Replace("'", "''")}'";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    var errorMessage = string.Join("; ", errors);
                    if (string.IsNullOrWhiteSpace(errorMessage) || errorMessage.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"Rule '{ruleName}' did not exist, considering it a success.");
                        return true;
                    }

                    throw new Exception($"PowerShell errors: {errorMessage}");
                }

                logger.LogInfo($"Successfully removed rule with PowerShell: {ruleName}");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"PowerShell method failed for removing rule '{ruleName}': {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RemoveSingleRuleWithNetsh(string ruleName, ILoggingService logger, CancellationToken cancellationToken = default)
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

            try
            {
                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        logger.LogError($"Failed to start netsh process for removing rule: {ruleName}");
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
                    else if (error.Contains("No rules match the specified criteria", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInfo($"No rule to remove with netsh for '{ruleName}', considering it a success.");
                        return true;
                    }
                    else
                    {
                        throw new InvalidOperationException($"netsh exit code: {process.ExitCode}, Output: {output}, Error: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"netsh method also failed for removing rule '{ruleName}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateSystemRestorePoint(string description, ILoggingService logger, CancellationToken cancellationToken = default)
        {
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, logger, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                logger.LogInfo("Attempting to create a system restore point...");
                var script = $"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS'";
                powerShell.AddScript(script);

                await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    throw new Exception($"PowerShell errors: {string.Join("; ", errors)}");
                }

                logger.LogInfo("System restore point created successfully.");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to create system restore point.", ex);
                return false;
            }
        }

        public async Task<List<AppIntBlockerGUI.Models.FirewallRuleModel>> GetAllFirewallRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = new List<AppIntBlockerGUI.Models.FirewallRuleModel>();
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, this.loggingService, cancellationToken).ConfigureAwait(false))
                {
                    return rules; // Error logged in module import
                }

                var script = $"Get-NetFirewallRule | Select-Object DisplayName, Name, Enabled, Direction, Action, Program";
                powerShell.AddScript(script);

                var results = await powerShell.InvokeAsync();

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Errors.Select(e => e.Exception?.Message).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    this.loggingService.LogError($"Error getting all firewall rules: {string.Join("; ", errors)}");
                    return rules;
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

        public async Task<bool> DeleteRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            using var powerShell = this.powerShellWrapperFactory();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, this.loggingService, cancellationToken).ConfigureAwait(false))
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
                if (!await this.ImportFirewallModules(powerShell, this.loggingService, cancellationToken).ConfigureAwait(false))
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
