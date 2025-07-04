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

    public class FirewallService : IFirewallService
    {
        private const string RuleNamePrefix = "AppBlocker Rule - ";
        private readonly ILoggingService _loggingService;
        private readonly ObjectPool<PowerShell> _powerShellPool;

            public FirewallService(ILoggingService loggingService, ObjectPool<PowerShell> powerShellPool)
    {
        _loggingService = loggingService;
        _powerShellPool = powerShellPool;
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
            var powerShell = _powerShellPool.Get();
            try
            {
                if (!await this.ImportFirewallModules(powerShell, logger).ConfigureAwait(false))
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
                    await this.CreateFirewallRule(powerShell, file, ruleName, "Inbound", logger).ConfigureAwait(false);
                    await this.CreateFirewallRule(powerShell, file, ruleName, "Outbound", logger).ConfigureAwait(false);
                }

                logger.LogInfo($"Block operation completed for {applicationName}.");
                return true;
            }
            finally
            {
                _powerShellPool.Return(powerShell);
            }
        }

        private async Task<bool> ImportFirewallModules(PowerShell powerShell, ILoggingService logger)
        {
            try
            {
                // Set execution policy to bypass for this session
                powerShell.Commands.Clear();
                powerShell.AddScript("Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force");
                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    logger.LogWarning($"Warning setting execution policy: {string.Join("; ", errors.Select(e => e.ToString()))}");
                }

                powerShell.Streams.Error.Clear();

                // Import NetSecurity module for firewall cmdlets
                powerShell.Commands.Clear();
                powerShell.AddScript("Import-Module NetSecurity -Force");
                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    logger.LogError($"CRITICAL: NetSecurity module import failed. Firewall operations cannot proceed: {string.Join("; ", errors.Select(e => e.ToString()))}");
                    return false;  // Signal failure clearly
                }

                powerShell.Streams.Error.Clear();
                return true;  // Success
            }
            catch (Exception ex)
            {
                logger.LogError("CRITICAL: Error importing PowerShell modules. Firewall operations cannot proceed.", ex);
                return false;  // Signal failure clearly
            }
        }

        private async Task<bool> CreateFirewallRule(PowerShell powerShell, string filePath, string displayName, string direction, ILoggingService logger)
        {
            try
            {
                // Try PowerShell first
                if (await this.TryCreateFirewallRuleWithPowerShell(powerShell, filePath, displayName, direction, logger).ConfigureAwait(false))
                {
                    return true;
                }

                // Fallback to netsh command
                logger.LogInfo($"PowerShell failed, trying netsh for rule: {displayName}");
                return await this.CreateFirewallRuleWithNetsh(filePath, displayName, direction, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception creating firewall rule '{displayName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryCreateFirewallRuleWithPowerShell(PowerShell powerShell, string filePath, string displayName, string direction, ILoggingService logger)
        {
            try
            {
                powerShell.Commands.Clear();
                powerShell.AddCommand("New-NetFirewallRule")
                    .AddParameter("DisplayName", displayName)
                    .AddParameter("Direction", direction)
                    .AddParameter("Program", filePath)
                    .AddParameter("Action", "Block")
                    .AddParameter("ErrorAction", "Stop");

                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
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

        private async Task<bool> CreateFirewallRuleWithNetsh(string filePath, string displayName, string direction, ILoggingService logger)
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

                    var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                    await process.WaitForExitAsync().ConfigureAwait(false);

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

        public async Task<bool> RemoveExistingRules(string applicationName, ILoggingService logger)
        {
            try
            {
                logger.LogInfo($"Removing firewall rules for application: {applicationName}");

                // Try PowerShell first
                if (await this.TryRemoveRulesWithPowerShell(applicationName, logger).ConfigureAwait(false))
                {
                    return true;
                }

                // Fallback to netsh
                logger.LogInfo("PowerShell removal failed, trying netsh approach...");
                return await this.RemoveRulesWithNetsh(applicationName, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error removing rules for '{applicationName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryRemoveRulesWithPowerShell(string applicationName, ILoggingService logger)
        {
            var powerShell = _powerShellPool.Get();
            try
            {
                // Import required modules
                await this.ImportFirewallModules(powerShell, logger).ConfigureAwait(false);

                // First, get ALL rules and filter manually (wildcard doesn't work reliably)
                powerShell.Commands.Clear();
                powerShell.AddCommand("Get-NetFirewallRule")
                    .AddParameter("ErrorAction", "SilentlyContinue");

                var allRules = await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    logger.LogWarning($"PowerShell errors getting rules: {string.Join("; ", errors.Select(e => e.ToString()))}");
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
                            powerShell.Commands.Clear();
                            powerShell.AddCommand("Remove-NetFirewallRule")
                                .AddParameter("DisplayName", displayName)
                                .AddParameter("ErrorAction", "Stop");

                            await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                            if (!powerShell.HadErrors)
                            {
                                removedCount++;
                                logger.LogInfo($"Successfully removed rule with PowerShell: {displayName}");
                            }
                            else
                            {
                                var errors = powerShell.Streams.Error.ReadAll();
                                logger.LogWarning($"PowerShell error removing rule '{displayName}': {string.Join("; ", errors.Select(e => e.ToString()))}");
                            }

                            powerShell.Streams.Error.Clear();
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
                _powerShellPool.Return(powerShell);
            }
        }

        private async Task<bool> RemoveRulesWithNetsh(string applicationName, ILoggingService logger)
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

                    var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    await process.WaitForExitAsync().ConfigureAwait(false);

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

                            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                            await process.WaitForExitAsync().ConfigureAwait(false);

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

        public async Task<List<string>> GetExistingRulesAsync(ILoggingService logger)
        {
            try
            {
                logger.LogInfo("Getting all firewall rules and filtering for AppIntBlocker...");

                var powerShell = _powerShellPool.Get();
                try
                {
                    // Import modules first
                    await this.ImportFirewallModules(powerShell, logger);

                    // Get ALL firewall rules (more reliable than wildcard)
                    powerShell.Commands.Clear();
                    powerShell.AddCommand("Get-NetFirewallRule")
                        .AddParameter("ErrorAction", "SilentlyContinue");

                    var allRules = await Task.Run(() => powerShell.Invoke());

                    if (powerShell.HadErrors)
                    {
                        var errors = powerShell.Streams.Error.ReadAll();
                        logger.LogWarning($"PowerShell warnings getting rules: {string.Join("; ", errors.Select(e => e.ToString()))}");
                    }

                    // Filter for AppIntBlocker rules manually (more reliable)
                    var appBlockerRules = new List<string>();
                    foreach (var rule in allRules)
                    {
                        try
                        {
                            var displayName = rule.Properties["DisplayName"]?.Value?.ToString();
                            if (!string.IsNullOrEmpty(displayName) && displayName.StartsWith(RuleNamePrefix))
                            {
                                appBlockerRules.Add(displayName);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Error processing individual rule: {ex.Message}");
                        }
                    }

                    logger.LogInfo($"Found {appBlockerRules.Count} AppIntBlocker rules out of {allRules.Count} total rules");
                    return appBlockerRules;
                }
                finally
                {
                    _powerShellPool.Return(powerShell);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error getting existing firewall rules with PowerShell, trying netsh fallback", ex);

                // Fallback to netsh method
                return await this.GetExistingRulesWithNetsh(logger);
            }
        }

        private async Task<List<string>> GetExistingRulesWithNetsh(ILoggingService logger)
        {
            try
            {
                logger.LogInfo("Using netsh to get firewall rules...");

                var listProcess = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=all",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var appBlockerRules = new List<string>();

                using (var process = Process.Start(listProcess))
                {
                    if (process == null)
                    {
                        logger.LogError("Failed to start netsh process for getting existing rules");
                        return new List<string>();
                    }

                    var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    await process.WaitForExitAsync().ConfigureAwait(false);

                    // Parse output to find AppIntBlocker rules
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Rule Name:"))
                        {
                            var ruleName = line.Substring("Rule Name:".Length).Trim();
                            if (!string.IsNullOrEmpty(ruleName) && ruleName.StartsWith(RuleNamePrefix))
                            {
                                appBlockerRules.Add(ruleName);
                            }
                        }
                    }
                }

                logger.LogInfo($"Found {appBlockerRules.Count} AppIntBlocker rules using netsh");
                return appBlockerRules;
            }
            catch (Exception ex)
            {
                logger.LogError("netsh fallback also failed", ex);
                return new List<string>();
            }
        }

        public async Task<bool> RemoveSingleRule(string ruleName, ILoggingService logger)
        {
            try
            {
                logger.LogInfo($"Removing single firewall rule: {ruleName}");

                // First try PowerShell
                if (await this.TryRemoveSingleRuleWithPowerShell(ruleName, logger))
                {
                    return true;
                }

                // Fallback to netsh
                logger.LogInfo("PowerShell failed, trying netsh for single rule removal");
                return await this.RemoveSingleRuleWithNetsh(ruleName, logger);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error removing single rule '{ruleName}'", ex);
                return false;
            }
        }

        private async Task<bool> TryRemoveSingleRuleWithPowerShell(string ruleName, ILoggingService logger)
        {
            var powerShell = _powerShellPool.Get();
            try
            {
                await this.ImportFirewallModules(powerShell, logger).ConfigureAwait(false);

                powerShell.Commands.Clear();
                powerShell.AddCommand("Remove-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
                    logger.LogWarning($"PowerShell error removing rule '{ruleName}': {errorMessage}");
                    return false;
                }

                logger.LogInfo($"Successfully removed rule with PowerShell: {ruleName}");
                return true;
            }
            finally
            {
                _powerShellPool.Return(powerShell);
            }
        }

        private async Task<bool> RemoveSingleRuleWithNetsh(string ruleName, ILoggingService logger)
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

                    var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    await process.WaitForExitAsync().ConfigureAwait(false);

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

        public async Task<bool> CreateSystemRestorePoint(string description, ILoggingService logger)
        {
            var powerShell = _powerShellPool.Get();
            try
            {
                logger.LogInfo($"Creating system restore point: {description}");

                powerShell.Commands.Clear();
                powerShell.AddCommand("Checkpoint-Computer")
                    .AddParameter("Description", description)
                    .AddParameter("RestorePointType", "MODIFY_SETTINGS")
                    .AddParameter("ErrorAction", "Stop");

                await Task.Run(() => powerShell.Invoke());

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
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
            finally
            {
                _powerShellPool.Return(powerShell);
            }
        }

        public async Task<List<AppIntBlockerGUI.Models.FirewallRuleModel>> GetAllFirewallRulesAsync()
        {
            var rules = new List<AppIntBlockerGUI.Models.FirewallRuleModel>();
            var powerShell = _powerShellPool.Get();

            try
            {
                // Get all firewall rules
                powerShell.Commands.Clear();
                powerShell.AddCommand("Get-NetFirewallRule")
                    .AddParameter("ErrorAction", "SilentlyContinue");

                var psResults = await Task.Run(() => powerShell.Invoke());

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
            finally
            {
                _powerShellPool.Return(powerShell);
            }

            return rules;
        }

        public async Task<bool> DeleteRuleAsync(string ruleName)
        {
            var powerShell = _powerShellPool.Get();
            try
            {
                powerShell.Commands.Clear();
                powerShell.AddCommand("Remove-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
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
            finally
            {
                _powerShellPool.Return(powerShell);
            }
        }

        public async Task<bool> ToggleRuleAsync(string ruleName, bool enable)
        {
            var powerShell = _powerShellPool.Get();
            try
            {
                powerShell.Commands.Clear();
                var action = enable ? "Enable" : "Disable";
                powerShell.AddCommand($"{action}-NetFirewallRule")
                    .AddParameter("DisplayName", ruleName)
                    .AddParameter("ErrorAction", "Stop");

                await Task.Run(() => powerShell.Invoke()).ConfigureAwait(false);

                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessage = string.Join("; ", errors.Select(e => e.ToString()));
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
            finally
            {
                _powerShellPool.Return(powerShell);
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
