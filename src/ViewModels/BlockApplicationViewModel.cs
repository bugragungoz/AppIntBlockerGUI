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
                // Style 1: Standard Block
                @"
 CCCCC   RRRRRR   OOOOO   XXX   XXX   ZZZZZZZ
CC    C  R     R O     O   XXX XXX       ZZZ
CC       R     R O     O    XX XX       ZZZ
CC       RRRRRR  O     O     XXX       ZZZ
CC       R   R   O     O    XX XX     ZZZ
CC    C  R    R  O     O   XXX XXX   ZZZ
 CCCCC   R     R  OOOOO   XXX   XXX  ZZZZZZZ",

                // Style 2: Slant
                @"
   CCCCC   RRRRRR    OOOOO    XXX   XXX  ZZZZZZZ
  CC    C  R     R  O     O    XXX XXX      ZZZ
 CC        R     R  O     O     XX XX      ZZZ
 CC        RRRRRR   O     O      XXX      ZZZ
 CC        R   R    O     O     XX XX    ZZZ
  CC    C  R    R   O     O    XXX XXX  ZZZ
   CCCCC   R     R   OOOOO    XXX   XXX ZZZZZZZ",
                
                // Style 3: Dotted
                @"
 oooooo   o--o   o----o   o   o   o---o
o      o  o   o  o      o   o o   o     
o         o--o   o      o    o    o----
o         o  o   o      o   o o        o
o      o  o   o  o      o  o   o  o     o
 oooooo   o   o  o----o   o   o   o---o",

                // Style 4: Modern Lines
                @"
  _______  ______    _______   __   __   _______
 /  ____/ /  __  \  /  ____/  |  | |  | /  ____/
|  /      | |  | | |  /       |  | |  | |  /____
| |       | |__| | | |        |  |_|  | \____   \
|  \____  |   __/  |  \____   \______/  ____/   |
 \______\ |__|      \_______\         /_______/ ",

                // Style 5: Cyber
                @"
 [CCCCCC] [RRRRR]  [OOOOO]  [X]   [X] [ZZZZZZZ]
[CC]      [RR]  [RR][OO] [OO]  [X] [X]      [ZZ]
[CC]      [RR]  [RR][OO] [OO]   [X]X       [ZZ]
[CC]      [RRRRR]  [OO] [OO]    [X]       [ZZ]
[CC]      [RR] [RR][OO] [OO]   [X]X      [ZZ]
[CC]      [RR]  [RR][OO] [OO]  [X] [X]   [ZZ]
 [CCCCCC] [RR]  [RR] [OOOOO]  [X]   [X] [ZZZZZZZ]"
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

        [RelayCommand]
        private async Task BlockApplicationAsync()
        {
            if (string.IsNullOrWhiteSpace(ApplicationPath))
            {
                _dialogService.ShowError("Please select an application to block.");
                return;
            }

            if (string.IsNullOrWhiteSpace(OperationName))
            {
                OperationName = $"Block_{FolderName}_{DateTime.Now:HHmmss}";
            }

            try
            {
                IsOperationInProgress = true;
                CurrentOperationStatus = "Blocking application...";
                
                // Start time estimation
                _ = Task.Run(() => UpdateTimeEstimation());

                var excludedKeywords = UseExclusions ? 
                    ExcludedKeywords.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList() : 
                    new List<string>();
                
                var excludedFiles = UseExclusions ? 
                    ExcludedFiles.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToList() : 
                    new List<string>();

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
                _dialogService.ShowInfo($"Application '{FolderName}' has been blocked successfully.");
            }
            catch (Exception ex)
            {
                CurrentOperationStatus = "Operation failed";
                _loggingService.LogError($"Failed to block application: {ex.Message}");
                _dialogService.ShowError($"Failed to block application: {ex.Message}");
            }
            finally
            {
                IsOperationInProgress = false;
                EstimatedTimeRemaining = "00:00";
            }
        }

        private async Task UpdateTimeEstimation()
        {
            var elapsed = TimeSpan.Zero;
            var estimatedTotal = TimeSpan.FromSeconds(30); // Base estimate

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // CRITICAL FIX: Check both cancellation token and operation status
                    if (!IsOperationInProgress)
                        break;

                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                    elapsed = elapsed.Add(TimeSpan.FromSeconds(1));
                    
                    var remaining = estimatedTotal - elapsed;
                    if (remaining.TotalSeconds > 0)
                    {
                        EstimatedTimeRemaining = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                    }
                    else
                    {
                        EstimatedTimeRemaining = "Finalizing...";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, this is expected
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

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
            _loggingService.LogEntryAdded -= OnLogEntryAdded;
        }
    }
} 