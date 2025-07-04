using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;
using AppIntBlockerGUI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;
using System.Security;
using System.Text.RegularExpressions;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class BlockApplicationViewModel : ObservableObject, IDisposable
    {
        private readonly IFirewallService _firewallService;
        private readonly ILoggingService _loggingService;
        private readonly IDialogService _dialogService;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private int _welcomeMessageLogIndex = -1;

        // Path and Operation Properties
        [ObservableProperty] private string _operationName = "";
        [ObservableProperty] private string _applicationPath = "";

        public string FullRuleName => string.IsNullOrWhiteSpace(OperationName) ? "N/A" : $"AIB_{OperationName}";

        // Block Settings Properties
        [ObservableProperty] private bool _includeExeFiles = true;
        [ObservableProperty] private bool _includeDllFiles = true;
        [ObservableProperty] private bool _includeSubfolders = true;
        [ObservableProperty] private bool _createRestorePoint = true;
        [ObservableProperty] private bool _useExclusions = false;
        [ObservableProperty] private bool _enableDetailedLogging = false;
        
        // Exclusion Properties
        [ObservableProperty] private string _excludedKeywords = "";
        [ObservableProperty] private string _excludedFiles = "";

        // File Information Properties
        [ObservableProperty] private string _folderName = "N/A";
        [ObservableProperty] private string _subfoldersIncluded = "No";
        [ObservableProperty] private string _executableFilesCount = "0";
        [ObservableProperty] private string _libraryFilesCount = "0";

        // Operation Status Properties
        [ObservableProperty] private string _currentOperationStatus = "Waiting for operation";
        [ObservableProperty] private bool _isOperationInProgress = false;
        [ObservableProperty] private string _estimatedTimeRemaining = "00:00";

        // Activity Terminal Properties
        [ObservableProperty] private string _asciiArt = "";
        [ObservableProperty] private string _welcomeMessage = "";
        public ObservableCollection<string> LogEntries { get; } = new ObservableCollection<string>();

        // --- Compatibility Properties for refactored code ---
        public string StatusMessage { get => CurrentOperationStatus; set => CurrentOperationStatus = value; }
        public bool IsBlocking { get => IsOperationInProgress; set => IsOperationInProgress = value; }
        public ObservableCollection<string> LogItems => LogEntries;
        public int Progress { get; set; }
        // --- End Compatibility Properties ---

        public BlockApplicationViewModel(
            IFirewallService firewallService, 
            ILoggingService loggingService, 
            IDialogService dialogService)
        {
            _firewallService = firewallService;
            _loggingService = loggingService;
            _dialogService = dialogService;
            _loggingService.LogEntryAdded += OnLogEntryAdded;
        }

        public async Task InitializeAsync()
        {
            InitializeAsciiArt();
            InitializeWelcomeMessage();

            // This must be on the UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                InitializeTerminal();
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
            AsciiArt = styles[random.Next(styles.Length)];
        }

        private void InitializeWelcomeMessage()
        {
            WelcomeMessage = "Welcome to AppIntBlocker v1.0 - Advanced Application Internet Access Control";
        }

        private void InitializeTerminal()
        {
            LogEntries.Clear();
            // Add ASCII art line by line
            var asciiLines = AsciiArt.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in asciiLines)
            {
                LogEntries.Add(line);
            }
            
            LogEntries.Add(""); // Spacer
            LogEntries.Add(""); // Extra Spacer
            LogEntries.Add(""); // Extra Spacer
            
            _welcomeMessageLogIndex = LogEntries.Count; // Store the index
            LogEntries.Add(WelcomeMessage);

            LogEntries.Add(""); // Spacer
            LogEntries.Add("System initialized and ready to operate.");
        }

        private void OnLogEntryAdded(string logEntry)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (EnableDetailedLogging)
                {
                    // Show full detailed log entry
                    LogEntries.Add(logEntry);
                }
                else
                {
                    // Show simplified log entry (remove technical details)
                    var simplifiedEntry = SimplifyLogEntry(logEntry);
                    if (!string.IsNullOrEmpty(simplifiedEntry))
                    {
                        LogEntries.Add(simplifiedEntry);
                    }
                }
                
                // Keep terminal from getting too long
                while (LogEntries.Count > 100)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        private string SimplifyLogEntry(string logEntry)
        {
            if (EnableDetailedLogging) return logEntry;

            if (logEntry.Contains("[DEBUG]")) return "";

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
                    return pair.Value;
            }

            if (logEntry.Contains("[ERROR]"))
                return $"ERROR: {ExtractMainErrorMessage(logEntry)}";
            if (logEntry.Contains("[SUCCESS]"))
                return logEntry;
            
            return ""; // Default to not showing the log
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
            var selectedPath = _dialogService.OpenFolderDialog();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ApplicationPath = selectedPath;
            }
        }

        private void AnalyzeSelectedFile()
        {
            if (string.IsNullOrWhiteSpace(ApplicationPath))
            {
                ResetFileInfo();
                return;
            }

            try
            {
                string directoryPath;
                if (File.Exists(ApplicationPath))
                {
                    directoryPath = Path.GetDirectoryName(ApplicationPath) ?? "";
                }
                else if (Directory.Exists(ApplicationPath))
                {
                    directoryPath = ApplicationPath;
                }
                else
                {
                    ResetFileInfo();
                    return;
                }
                
                if (string.IsNullOrEmpty(directoryPath)) 
                {
                    ResetFileInfo();
                    return;
                }

                var dirInfo = new DirectoryInfo(directoryPath);
                FolderName = dirInfo.Name;
                SubfoldersIncluded = IncludeSubfolders ? "Yes" : "No";

                var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                ExecutableFilesCount = dirInfo.GetFiles("*.exe", searchOption).Length.ToString();
                LibraryFilesCount = dirInfo.GetFiles("*.dll", searchOption).Length.ToString();

                _loggingService.LogInfo($"Folder analyzed: {FolderName}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to analyze folder: {ex.Message}");
                ResetFileInfo();
            }
        }

        private void ResetFileInfo()
        {
            FolderName = "N/A";
            SubfoldersIncluded = "No";
            ExecutableFilesCount = "0";
            LibraryFilesCount = "0";
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
        private bool ValidateOperationName(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                return true; // Empty is allowed, will be auto-generated

            // Check length
            if (operationName.Length > 100)
            {
                StatusMessage = "Error: Operation name is too long (maximum 100 characters).";
                return false;
            }

            // Only allow safe characters for rule names
            var validNameRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_. -]+$");
            if (!validNameRegex.IsMatch(operationName))
            {
                StatusMessage = "Error: Operation name contains invalid characters. Only letters, numbers, spaces, dots, hyphens, and underscores are allowed.";
                return false;
            }

            // Prevent malicious names
            var forbiddenPatterns = new[] { "cmd", "powershell", "netsh", "wmic", "reg", "schtasks", "..", "script", "execute" };
            var lowerName = operationName.ToLower();
            foreach (var pattern in forbiddenPatterns)
            {
                if (lowerName.Contains(pattern))
                {
                    StatusMessage = $"Error: Operation name cannot contain '{pattern}'.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates excluded files patterns to prevent injection attacks.
        /// AI-generated code: Enhanced input validation for security.
        /// </summary>
        private bool ValidateExcludedFiles(List<string> excludedFiles)
        {
            foreach (var file in excludedFiles)
            {
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                // Check length
                if (file.Length > 255)
                {
                    StatusMessage = $"Error: Excluded file name is too long: '{file}' (maximum 255 characters).";
                    return false;
                }

                // Only allow safe characters for file patterns
                var validFileRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_. *-]+$");
                if (!validFileRegex.IsMatch(file))
                {
                    StatusMessage = $"Error: Invalid character in excluded file pattern '{file}'.";
                    return false;
                }

                // Prevent malicious patterns
                if (file.Contains("..") || file.Contains("/") || file.Contains("\\"))
                {
                    StatusMessage = $"Error: Excluded file pattern cannot contain path separators or traversal: '{file}'.";
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
                StatusMessage = "Error: Administrator privileges are required to block applications.";
                return;
            }

            if (IsBlocking || string.IsNullOrWhiteSpace(ApplicationPath))
            {
                return;
            }

            if (!IsPathSafe(ApplicationPath))
            {
                // IsPathSafe sets the status message
                return;
            }

            // Validate operation name
            if (!ValidateOperationName(OperationName))
            {
                return;
            }

            IsBlocking = true;
            Progress = 0;

            if (string.IsNullOrWhiteSpace(OperationName))
            {
                OperationName = $"Block_{FolderName}_{DateTime.Now:HHmmss}";
            }

            try
            {
                IsOperationInProgress = true;
                CurrentOperationStatus = "Blocking application...";
                
                var excludedKeywords = new List<string>();
                if (UseExclusions)
                {
                    var keywords = ExcludedKeywords.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(k => k.Trim())
                                                   .Where(k => !string.IsNullOrWhiteSpace(k));
                    
                    // Enhanced keyword validation - more restrictive
                    var validKeywordRegex = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_.-]+$");
                    
                    foreach (var keyword in keywords)
                    {
                        // Additional length check
                        if (keyword.Length > 50)
                        {
                            StatusMessage = $"Error: Exclusion keyword is too long: '{keyword}' (maximum 50 characters).";
                            IsBlocking = false;
                            return;
                        }

                        if (validKeywordRegex.IsMatch(keyword))
                        {
                            excludedKeywords.Add(keyword);
                        }
                        else
                        {
                            StatusMessage = $"Error: Invalid character in exclusion keyword '{keyword}'. Only letters, numbers, dots, hyphens, and underscores are allowed.";
                            IsBlocking = false;
                            return;
                        }
                    }
                }

                var excludedFiles = new List<string>();
                if (UseExclusions && !string.IsNullOrWhiteSpace(ExcludedFiles))
                {
                    var files = ExcludedFiles.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(f => f.Trim())
                                            .Where(f => !string.IsNullOrWhiteSpace(f))
                                            .ToList();

                    // Validate excluded files
                    if (!ValidateExcludedFiles(files))
                    {
                        IsBlocking = false;
                        return;
                    }

                    excludedFiles.AddRange(files);
                }
                
                await _firewallService.BlockApplicationFiles(
                    ApplicationPath,
                    IncludeExeFiles,
                    IncludeDllFiles,
                    IncludeSubfolders,
                    excludedKeywords,
                    excludedFiles,
                    _loggingService,
                    _cancellationTokenSource.Token
                ).ConfigureAwait(false);

                CurrentOperationStatus = "Operation completed successfully";
                _dialogService.ShowMessage($"Application '{FolderName}' has been blocked successfully.", "Success");
            }
            catch (Exception ex)
            {
                CurrentOperationStatus = "Operation failed";
                _loggingService.LogError($"Failed to block application: {ex.Message}");
                _dialogService.ShowMessage($"Failed to block application: {ex.Message}", "Error");
            }
            finally
            {
                IsOperationInProgress = false;
                EstimatedTimeRemaining = "00:00";
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            InitializeTerminal();
            _loggingService.LogInfo("Terminal cleared by user");
        }

        partial void OnApplicationPathChanged(string value)
        {
            AnalyzeSelectedFile();
        }

        partial void OnIncludeSubfoldersChanged(bool value)
        {
            AnalyzeSelectedFile();
        }

        partial void OnOperationNameChanged(string value)
        {
            OnPropertyChanged(nameof(FullRuleName));
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
        private bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true; 

            try
            {
                // Normalize the path to prevent traversal attacks
                var fullPath = Path.GetFullPath(path);
                
                // Check for path traversal attempts (e.g., ../, ..\)
                if (path.Contains("..") || path.Contains("./") || path.Contains(".\\"))
                {
                    CurrentOperationStatus = "Error: Path traversal attempts are not allowed.";
                    _dialogService.ShowMessage(CurrentOperationStatus, "Security Warning");
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
                        CurrentOperationStatus = "Error: Blocking files in system directories is not permitted.";
                        _dialogService.ShowMessage(CurrentOperationStatus, "Security Warning");
                        return false;
                    }
                }
                
                // Check if path exists and is accessible
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    // Additional check for critical system files
                    if (fileInfo.Extension.Equals(".sys", StringComparison.OrdinalIgnoreCase) ||
                        fileInfo.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) && 
                        fileInfo.DirectoryName != null &&
                        fileInfo.DirectoryName.StartsWith(systemDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        CurrentOperationStatus = "Error: Blocking system files is not permitted.";
                        _dialogService.ShowMessage(CurrentOperationStatus, "Security Warning");
                        return false;
                    }
                }
                else if (Directory.Exists(fullPath))
                {
                    // Directory exists, additional checks passed above
                }
                else
                {
                    CurrentOperationStatus = "Error: The specified path does not exist.";
                    _dialogService.ShowMessage(CurrentOperationStatus, "Validation Error");
                    return false;
                }
            }
            catch (ArgumentException)
            {
                CurrentOperationStatus = "Error: The provided path contains invalid characters.";
                _dialogService.ShowMessage(CurrentOperationStatus, "Validation Error");
                return false;
            }
            catch (SecurityException)
            {
                CurrentOperationStatus = "Error: You do not have permission to access the specified path.";
                _dialogService.ShowMessage(CurrentOperationStatus, "Access Denied");
                return false;
            }
            catch (PathTooLongException)
            {
                CurrentOperationStatus = "Error: The specified path is too long.";
                _dialogService.ShowMessage(CurrentOperationStatus, "Validation Error");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                CurrentOperationStatus = "Error: Access to the path is denied.";
                _dialogService.ShowMessage(CurrentOperationStatus, "Access Denied");
                return false;
            }
            
            return true;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            _loggingService.LogEntryAdded -= OnLogEntryAdded;
        }
    }
} 