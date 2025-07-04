// <copyright file="BlockApplicationViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using AppIntBlockerGUI.Models;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public partial class BlockApplicationViewModel : ObservableObject, IDisposable
    {
        private readonly IFirewallService firewallService;
        private readonly ILoggingService loggingService;
        private readonly IDialogService dialogService;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private int welcomeMessageLogIndex = -1;

        // Path and Operation Properties
        [ObservableProperty]
        private string operationName = string.Empty;
        [ObservableProperty]
        private string applicationPath = string.Empty;

        public string FullRuleName => string.IsNullOrWhiteSpace(this.OperationName) ? "N/A" : $"AIB_{this.OperationName}";

        // Block Settings Properties
        [ObservableProperty]
        private bool includeExeFiles = true;
        [ObservableProperty]
        private bool includeDllFiles = true;
        [ObservableProperty]
        private bool includeSubfolders = true;
        [ObservableProperty]
        private bool createRestorePoint = true;
        [ObservableProperty]
        private bool useExclusions = false;
        [ObservableProperty]
        private bool enableDetailedLogging = false;

        // Exclusion Properties
        [ObservableProperty]
        private string excludedKeywords = string.Empty;
        [ObservableProperty]
        private string excludedFiles = string.Empty;

        // File Information Properties
        [ObservableProperty]
        private string folderName = "N/A";
        [ObservableProperty]
        private string subfoldersIncluded = "No";
        [ObservableProperty]
        private string executableFilesCount = "0";
        [ObservableProperty]
        private string libraryFilesCount = "0";

        // Operation Status Properties
        [ObservableProperty]
        private string currentOperationStatus = "Waiting for operation";
        [ObservableProperty]
        private bool isOperationInProgress = false;
        [ObservableProperty]
        private string estimatedTimeRemaining = "00:00";

        // Activity Terminal Properties
        [ObservableProperty]
        private string asciiArt = string.Empty;
        [ObservableProperty]
        private string welcomeMessage = string.Empty;

        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

        // --- Compatibility Properties for refactored code ---
        public string StatusMessage { get => this.CurrentOperationStatus; set => this.CurrentOperationStatus = value; }

        public bool IsBlocking { get => this.IsOperationInProgress; set => this.IsOperationInProgress = value; }

        public ObservableCollection<string> LogItems => this.LogEntries;

        public int Progress { get; set; }

        // --- End Compatibility Properties ---
        public BlockApplicationViewModel(
            IFirewallService firewallService,
            ILoggingService loggingService,
            IDialogService dialogService)
        {
            this.firewallService = firewallService;
            this.loggingService = loggingService;
            this.dialogService = dialogService;
            this.loggingService.LogEntryAdded += this.OnLogEntryAdded;
        }

        public async Task InitializeAsync()
        {
            this.InitializeAsciiArt();
            this.InitializeWelcomeMessage();

            // This must be on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.InitializeTerminal();
            });
        }

        private void InitializeAsciiArt()
        {
            var styles = new[]
            {
                // Style 1
                @"
   ___ _ __ _____  __ ____
 / __| '__/ _ \ \/ /|_  /
| (__| | | (_) >  <  / / 
 \___|_|  \___/_/\_\/___|",

                // Style 2
                @"
    _,.----.                 _,.---._           ,-.--,          
 .' .' -   \  .-.,.---.   ,-.' , -  `..--.-.  /=/, .' ,--,----.
/==/  ,  ,-' /==/  `   \ /==/_,  ,  - \==\ -\/=/- /  /==/` - ./
|==|-   |  .|==|-, .=., |==|   .=.     \==\ `-' ,/   `--`=/. / 
|==|_   `-' \==|   '='  /==|_ : ;=:  - ||==|,  - |    /==/- /  
|==|   _  , |==|- ,   .'|==| , '='     /==/   ,   \  /==/- /-. 
\==\.       /==|_  . ,'. \==\ -    ,_ /==/, .--, - \/==/, `--`\
 `-.`.___.-'/==/  /\ ,  ) '.='. -   .'\==\- \/=/ , /\==\-  -, |
            `--`-`--`--'    `--`--''   `--`-'  `--`  `--`.-.--`",

                // Style 3
                @"
  ,---.,--.--. ,---.,--.  ,--.,-----.
| .--'|  .--'| .-. |\  `'  / `-.  / 
\ `--.|  |   ' '-' '/  /.  \  /  `-.
 `---'`--'    `---''--'  '--'`-----'",

                // Style 4
                @"
  ___   _ __   ___   __  _  ____    
 /'___\/\`'__\/ __`\/\ \/'\/\_ ,`\  
/\ \__/\ \ \//\ \L\ \/>  </\/_/  /_ 
\ \____\\ \_\\ \____//\_/\_\ /\____\
 \/____/ \/_/ \/___/ \//\/_/ \/____/",

                // Style 5
                @"
 ▄████▄   ██▀███   ▒█████  ▒██   ██▒▒███████▒
▒██▀ ▀█  ▓██ ▒ ██▒▒██▒  ██▒▒▒ █ █ ▒░▒ ▒ ▒ ▄▀░
▒▓█    ▄ ▓██ ░▄█ ▒▒██░  ██▒░░  █   ░░ ▒ ▄▀▒░ 
▒▓▓▄ ▄██▒▒██▀▀█▄  ▒██   ██░ ░ █ █ ▒   ▄▀▒   ░
▒ ▓███▀ ░░██▓ ▒██▒░ ████▓▒░▒██▒ ▒██▒▒███████▒
░ ░▒ ▒  ░░ ▒▓ ░▒▓░░ ▒░▒░▒░ ▒▒ ░ ░▓ ░░▒▒ ▓░▒░▒
  ░  ▒     ░▒ ░ ▒░  ░ ▒ ▒░ ░░   ░▒ ░░░▒ ▒ ░ ▒
░          ░░   ░ ░ ░ ░ ▒   ░    ░  ░ ░ ░ ░ ░
░ ░         ░         ░ ░   ░    ░    ░ ░    
░                                   ░        "
            };

            var random = new Random();
            this.AsciiArt = styles[random.Next(styles.Length)];
        }

        private void InitializeWelcomeMessage()
        {
            this.WelcomeMessage = "Welcome to AppIntBlocker v1.0 - Advanced Application Internet Access Control";
        }

        private void InitializeTerminal()
        {
            this.LogEntries.Clear();

            // Add ASCII art line by line
            var asciiLines = this.AsciiArt.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in asciiLines)
            {
                this.LogEntries.Add(line);
            }

            this.LogEntries.Add(string.Empty); // Spacer
            this.LogEntries.Add(string.Empty); // Extra Spacer
            this.LogEntries.Add(string.Empty); // Extra Spacer

            this.welcomeMessageLogIndex = this.LogEntries.Count; // Store the index
            this.LogEntries.Add(this.WelcomeMessage);

            this.LogEntries.Add(string.Empty); // Spacer
            this.LogEntries.Add("System initialized and ready to operate.");
        }

        private void OnLogEntryAdded(string logEntry)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (this.EnableDetailedLogging)
                {
                    // Show full detailed log entry
                    this.LogEntries.Add(logEntry);
                }
                else
                {
                    // Show simplified log entry (remove technical details)
                    var simplifiedEntry = this.SimplifyLogEntry(logEntry);
                    if (!string.IsNullOrEmpty(simplifiedEntry))
                    {
                        this.LogEntries.Add(simplifiedEntry);
                    }
                }

                // Keep terminal from getting too long
                while (this.LogEntries.Count > 100)
                {
                    this.LogEntries.RemoveAt(0);
                }
            });
        }

        private string SimplifyLogEntry(string logEntry)
        {
            if (this.EnableDetailedLogging)
            {
                return logEntry;
            }

            if (logEntry.Contains("[DEBUG]"))
            {
                return string.Empty;
            }

            var keywordsToSimple = new Dictionary<string, string>
            {
                { "Creating firewall rule", "Creating protection rule..." },
                { "Rule created successfully", "Rule created successfully" },
                { "Operation completed", "Operation completed" },
                { "System initialized", "System initialized and ready to operate." }
            };

            foreach (var pair in keywordsToSimple)
            {
                if (logEntry.Contains(pair.Key))
                {
                    return pair.Value;
                }
            }

            if (logEntry.Contains("[ERROR]"))
            {
                return $"ERROR: {this.ExtractMainErrorMessage(logEntry)}";
            }

            if (logEntry.Contains("[SUCCESS]"))
            {
                return logEntry;
            }

            return string.Empty; // Default to not showing the log
        }

        private string ExtractMainErrorMessage(string logEntry)
        {
            // Extract main error message without technical details
            if (logEntry.Contains(":"))
            {
                var parts = logEntry.Split(':');
                return parts.Length > 1 ? parts[1].Trim() : logEntry;
            }

            return logEntry;
        }

        [RelayCommand]
        private void Browse()
        {
            // FIXED: Use dialog service instead of directly using Windows Forms
            var selectedPath = this.dialogService.OpenFolderDialog();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                this.ApplicationPath = selectedPath;
            }
        }

        private void AnalyzeSelectedFile()
        {
            if (string.IsNullOrWhiteSpace(this.ApplicationPath))
            {
                this.ResetFileInfo();
                return;
            }

            try
            {
                string directoryPath;
                if (File.Exists(this.ApplicationPath))
                {
                    directoryPath = Path.GetDirectoryName(this.ApplicationPath) ?? string.Empty;
                }
                else if (Directory.Exists(this.ApplicationPath))
                {
                    directoryPath = this.ApplicationPath;
                }
                else
                {
                    this.ResetFileInfo();
                    return;
                }

                if (string.IsNullOrEmpty(directoryPath))
                {
                    this.ResetFileInfo();
                    return;
                }

                var dirInfo = new DirectoryInfo(directoryPath);
                this.FolderName = dirInfo.Name;
                this.SubfoldersIncluded = this.IncludeSubfolders ? "Yes" : "No";

                var searchOption = this.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                this.ExecutableFilesCount = dirInfo.GetFiles("*.exe", searchOption).Length.ToString();
                this.LibraryFilesCount = dirInfo.GetFiles("*.dll", searchOption).Length.ToString();

                this.loggingService.LogInfo($"Folder analyzed: {this.FolderName}");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Failed to analyze folder: {ex.Message}");
                this.ResetFileInfo();
            }
        }

        private void ResetFileInfo()
        {
            this.FolderName = "N/A";
            this.SubfoldersIncluded = "No";
            this.ExecutableFilesCount = "0";
            this.LibraryFilesCount = "0";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Validates operation name to prevent injection attacks and ensure safe rule names.
        /// AI-generated code: Enhanced input validation for security.
        /// </summary>
        /// <returns></returns>
        private bool ValidateOperationName(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                return true; // Empty is allowed, will be auto-generated
            }

            // Check length
            if (operationName.Length > 100)
            {
                this.StatusMessage = "Error: Operation name is too long (maximum 100 characters).";
                return false;
            }

            // Only allow safe characters for rule names
            var validNameRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_. -]+$");
            if (!validNameRegex.IsMatch(operationName))
            {
                this.StatusMessage = "Error: Operation name contains invalid characters. Only letters, numbers, spaces, dots, hyphens, and underscores are allowed.";
                return false;
            }

            // Prevent malicious names
            var forbiddenPatterns = new[] { "cmd", "powershell", "netsh", "wmic", "reg", "schtasks", "..", "script", "execute" };
            var lowerName = operationName.ToLower();
            foreach (var pattern in forbiddenPatterns)
            {
                if (lowerName.Contains(pattern))
                {
                    this.StatusMessage = $"Error: Operation name cannot contain '{pattern}'.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates excluded files patterns to prevent injection attacks.
        /// AI-generated code: Enhanced input validation for security.
        /// </summary>
        /// <returns></returns>
        private bool ValidateExcludedFiles(List<string> excludedFiles)
        {
            foreach (var file in excludedFiles)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                // Check length
                if (file.Length > 255)
                {
                    this.StatusMessage = $"Error: Excluded file name is too long: '{file}' (maximum 255 characters).";
                    return false;
                }

                // Only allow safe characters for file patterns
                var validFileRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_. *-]+$");
                if (!validFileRegex.IsMatch(file))
                {
                    this.StatusMessage = $"Error: Invalid character in excluded file pattern '{file}'.";
                    return false;
                }

                // Prevent malicious patterns
                if (file.Contains("..") || file.Contains("/") || file.Contains("\\"))
                {
                    this.StatusMessage = $"Error: Excluded file pattern cannot contain path separators or traversal: '{file}'.";
                    return false;
                }
            }

            return true;
        }

        [RelayCommand]
        private async Task BlockApplication()
        {
            if (!App.IsRunAsAdministrator())
            {
                this.StatusMessage = "Error: Administrator privileges are required to block applications.";
                return;
            }

            if (this.IsBlocking || string.IsNullOrWhiteSpace(this.ApplicationPath))
            {
                return;
            }

            if (!this.IsPathSafe(this.ApplicationPath))
            {
                // IsPathSafe sets the status message
                return;
            }

            // Validate operation name
            if (!this.ValidateOperationName(this.OperationName))
            {
                return;
            }

            this.IsBlocking = true;
            this.Progress = 0;

            if (string.IsNullOrWhiteSpace(this.OperationName))
            {
                this.OperationName = $"Block_{this.FolderName}_{DateTime.Now:HHmmss}";
            }

            try
            {
                this.IsOperationInProgress = true;
                this.CurrentOperationStatus = "Blocking application...";

                var excludedKeywords = new List<string>();
                if (this.UseExclusions)
                {
                    var keywords = this.ExcludedKeywords.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(k => k.Trim())
                                                   .Where(k => !string.IsNullOrWhiteSpace(k));

                    // Enhanced keyword validation - more restrictive
                    var validKeywordRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_.-]+$");

                    foreach (var keyword in keywords)
                    {
                        // Additional length check
                        if (keyword.Length > 50)
                        {
                            this.StatusMessage = $"Error: Exclusion keyword is too long: '{keyword}' (maximum 50 characters).";
                            this.IsBlocking = false;
                            return;
                        }

                        if (validKeywordRegex.IsMatch(keyword))
                        {
                            excludedKeywords.Add(keyword);
                        }
                        else
                        {
                            this.StatusMessage = $"Error: Invalid character in exclusion keyword '{keyword}'. Only letters, numbers, dots, hyphens, and underscores are allowed.";
                            this.IsBlocking = false;
                            return;
                        }
                    }
                }

                var excludedFiles = new List<string>();
                if (this.UseExclusions && !string.IsNullOrWhiteSpace(this.ExcludedFiles))
                {
                    var files = this.ExcludedFiles.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(f => f.Trim())
                                            .Where(f => !string.IsNullOrWhiteSpace(f))
                                            .ToList();

                    // Validate excluded files
                    if (!this.ValidateExcludedFiles(files))
                    {
                        this.IsBlocking = false;
                        return;
                    }

                    excludedFiles.AddRange(files);
                }

                await this.firewallService.BlockApplicationFiles(
                    this.ApplicationPath,
                    this.IncludeExeFiles,
                    this.IncludeDllFiles,
                    this.IncludeSubfolders,
                    excludedKeywords,
                    excludedFiles,
                    this.loggingService,
                    this.cancellationTokenSource.Token).ConfigureAwait(false);

                this.CurrentOperationStatus = "Operation completed successfully";
                this.dialogService.ShowMessage($"Application '{this.FolderName}' has been blocked successfully.", "Success");
            }
            catch (Exception ex)
            {
                this.CurrentOperationStatus = "Operation failed";
                this.loggingService.LogError($"Failed to block application: {ex.Message}");
                this.dialogService.ShowMessage($"Failed to block application: {ex.Message}", "Error");
            }
            finally
            {
                this.IsOperationInProgress = false;
                this.EstimatedTimeRemaining = "00:00";
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            this.LogEntries.Clear();
            this.InitializeTerminal();
            this.loggingService.LogInfo("Terminal cleared by user");
        }

        partial void OnApplicationPathChanged(string value)
        {
            this.AnalyzeSelectedFile();
        }

        partial void OnIncludeSubfoldersChanged(bool value)
        {
            this.AnalyzeSelectedFile();
        }

        partial void OnOperationNameChanged(string value)
        {
            this.OnPropertyChanged(nameof(this.FullRuleName));
        }

        [RelayCommand]
        private void ResetSettings()
        {
            // ... implementation ...
        }

        /// <summary>
        /// Validates if a path is safe to use, preventing path traversal attacks and access to system directories.
        /// AI-generated code: Enhanced security validation to prevent path traversal vulnerabilities.
        /// </summary>
        /// <returns></returns>
        private bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            try
            {
                // Normalize the path to prevent traversal attacks
                var fullPath = Path.GetFullPath(path);

                // Check for path traversal attempts (e.g., ../, ..\)
                if (path.Contains("..") || path.Contains("./") || path.Contains(".\\"))
                {
                    this.CurrentOperationStatus = "Error: Path traversal attempts are not allowed.";
                    this.dialogService.ShowMessage(this.CurrentOperationStatus, "Security Warning");
                    return false;
                }

                // Get system directories
                var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var programFilesDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86Directory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                // Prevent access to critical system directories
                var forbiddenDirectories = new[]
                {
                    windowsDirectory,
                    systemDirectory,
                    Path.Combine(windowsDirectory, "System32"),
                    Path.Combine(windowsDirectory, "SysWOW64"),
                    Path.Combine(windowsDirectory, "Boot"),
                    Path.Combine(windowsDirectory, "Recovery")
                };

                foreach (var forbiddenDir in forbiddenDirectories)
                {
                    if (!string.IsNullOrEmpty(forbiddenDir) &&
                        fullPath.StartsWith(forbiddenDir, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CurrentOperationStatus = "Error: Blocking files in system directories is not permitted.";
                        this.dialogService.ShowMessage(this.CurrentOperationStatus, "Security Warning");
                        return false;
                    }
                }

                // Check if path exists and is accessible
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);

                    // Additional check for critical system files
                    if (fileInfo.Extension.Equals(".sys", StringComparison.OrdinalIgnoreCase) ||
                        (fileInfo.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                        fileInfo.DirectoryName != null &&
                        fileInfo.DirectoryName.StartsWith(systemDirectory, StringComparison.OrdinalIgnoreCase)))
                    {
                        this.CurrentOperationStatus = "Error: Blocking system files is not permitted.";
                        this.dialogService.ShowMessage(this.CurrentOperationStatus, "Security Warning");
                        return false;
                    }
                }
                else if (Directory.Exists(fullPath))
                {
                    // Directory exists, additional checks passed above
                }
                else
                {
                    this.CurrentOperationStatus = "Error: The specified path does not exist.";
                    this.dialogService.ShowMessage(this.CurrentOperationStatus, "Validation Error");
                    return false;
                }
            }
            catch (ArgumentException)
            {
                this.CurrentOperationStatus = "Error: The provided path contains invalid characters.";
                this.dialogService.ShowMessage(this.CurrentOperationStatus, "Validation Error");
                return false;
            }
            catch (SecurityException)
            {
                this.CurrentOperationStatus = "Error: You do not have permission to access the specified path.";
                this.dialogService.ShowMessage(this.CurrentOperationStatus, "Access Denied");
                return false;
            }
            catch (PathTooLongException)
            {
                this.CurrentOperationStatus = "Error: The specified path is too long.";
                this.dialogService.ShowMessage(this.CurrentOperationStatus, "Validation Error");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                this.CurrentOperationStatus = "Error: Access to the path is denied.";
                this.dialogService.ShowMessage(this.CurrentOperationStatus, "Access Denied");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Dispose();
            this.loggingService.LogEntryAdded -= this.OnLogEntryAdded;
        }
    }
}
