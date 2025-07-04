using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;
using System.Diagnostics;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly ILoggingService _loggingService;

        // General Settings
        [ObservableProperty]
        private bool autoCreateRestorePoint = true;

        [ObservableProperty]
        private bool enableDetailedLogging = true;

        [ObservableProperty]
        private bool showConfirmationDialogs = true;

        [ObservableProperty]
        private bool autoRefreshStatistics = true;

        [ObservableProperty]
        private bool defaultIncludeSubdirectories = true;

        [ObservableProperty]
        private bool defaultBlockExeFiles = true;

        [ObservableProperty]
        private bool defaultBlockDllFiles = false;

        [ObservableProperty]
        private bool startMinimized = false;

        // Performance Settings
        [ObservableProperty]
        private int statisticsRefreshInterval = 30;

        [ObservableProperty]
        private int logCleanupDays = 30;

        [ObservableProperty]
        private bool preferPowerShell = true;

        [ObservableProperty]
        private bool cacheFirewallRules = true;

        [ObservableProperty]
        private bool runInBackground = true;

        [ObservableProperty]
        private bool limitLogOutput = true;

        // Default Exclusions
        [ObservableProperty]
        private bool enableExclusionsByDefault = true;

        [ObservableProperty]
        private string defaultExcludedKeywords = "unins, setup, install, update, temp, cache";

        [ObservableProperty]
        private string defaultExcludedFiles = "uninstall.exe, setup.exe, installer.exe";

        // Advanced Settings
        [ObservableProperty]
        private string customRulePrefix = "AppBlocker Rule";

        [ObservableProperty]
        private bool debugMode = false;

        [ObservableProperty]
        private bool exportSettingsOnExit = false;

        [ObservableProperty]
        private string backupLocation = "";

        [ObservableProperty]
        private bool autoBackup = false;

        [ObservableProperty]
        private bool checkForUpdates = true;

        // Application Info
        [ObservableProperty]
        private string appVersion = "1.0.0";

        [ObservableProperty]
        private string buildDate = "2024-01-01";

        [ObservableProperty]
        private string settingsLocation = "";

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            _dialogService = new DialogService();
            _loggingService = new LoggingService();

            InitializeApplicationInfo();
            LoadSettings();
        }

        private void InitializeApplicationInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                AppVersion = version?.ToString() ?? "1.0.0";

                var buildDate = File.GetCreationTime(assembly.Location);
                BuildDate = buildDate.ToString("yyyy-MM-dd");

                SettingsLocation = _settingsService.GetSettingsFilePath();

                // Set default backup location
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                BackupLocation = Path.Combine(appDataPath, "AppIntBlocker", "Backups");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error initializing application info", ex);
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();

                // Load general settings
                AutoCreateRestorePoint = settings.CreateRestorePoint;
                EnableDetailedLogging = settings.EnableDetailedLogging;
                DefaultIncludeSubdirectories = settings.IncludeSubdirectories;
                DefaultBlockExeFiles = settings.BlockExeFiles;
                DefaultBlockDllFiles = settings.BlockDllFiles;
                EnableExclusionsByDefault = settings.UseExclusions;
                DefaultExcludedKeywords = settings.ExcludedKeywords;
                DefaultExcludedFiles = settings.ExcludedFiles;

                _loggingService.LogInfo("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error loading settings", ex);
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                var settings = new Models.AppSettings
                {
                    CreateRestorePoint = AutoCreateRestorePoint,
                    EnableDetailedLogging = EnableDetailedLogging,
                    IncludeSubdirectories = DefaultIncludeSubdirectories,
                    BlockExeFiles = DefaultBlockExeFiles,
                    BlockDllFiles = DefaultBlockDllFiles,
                    UseExclusions = EnableExclusionsByDefault,
                    ExcludedKeywords = DefaultExcludedKeywords,
                    ExcludedFiles = DefaultExcludedFiles
                };

                _settingsService.SaveSettings(settings);
                
                _dialogService.ShowMessage("Settings saved successfully!", "Settings Saved");
                _loggingService.LogInfo("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error saving settings", ex);
                _dialogService.ShowMessage("Failed to save settings. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void ResetSettings()
        {
            try
            {
                var confirmMessage = "This will reset all settings to their default values.\n\n" +
                                   "Are you sure you want to continue?";

                if (!_dialogService.ShowConfirmation(confirmMessage, "Reset Settings"))
                {
                    return;
                }

                // Reset to default values
                AutoCreateRestorePoint = true;
                EnableDetailedLogging = true;
                ShowConfirmationDialogs = true;
                AutoRefreshStatistics = true;
                DefaultIncludeSubdirectories = true;
                DefaultBlockExeFiles = true;
                DefaultBlockDllFiles = false;
                StartMinimized = false;

                StatisticsRefreshInterval = 30;
                LogCleanupDays = 30;
                PreferPowerShell = true;
                CacheFirewallRules = true;
                RunInBackground = true;
                LimitLogOutput = true;

                EnableExclusionsByDefault = true;
                DefaultExcludedKeywords = "unins, setup, install, update, temp, cache";
                DefaultExcludedFiles = "uninstall.exe, setup.exe, installer.exe";

                CustomRulePrefix = "AppBlocker Rule";
                DebugMode = false;
                ExportSettingsOnExit = false;
                AutoBackup = false;
                CheckForUpdates = true;

                _dialogService.ShowMessage("Settings have been reset to default values.", "Settings Reset");
                _loggingService.LogInfo("Settings reset to default values");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error resetting settings", ex);
                _dialogService.ShowMessage("Failed to reset settings. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void BrowseBackupLocation()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var selectedPath = _dialogService.OpenFolderDialog();

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    BackupLocation = selectedPath;
                    _loggingService.LogInfo($"Backup location changed to: {BackupLocation}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error browsing for backup location", ex);
                _dialogService.ShowError("Failed to browse for backup location.");
            }
        }

        [RelayCommand]
        private void OpenLogsFolder()
        {
            try
            {
                var logsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                
                if (!Directory.Exists(logsFolder))
                {
                    Directory.CreateDirectory(logsFolder);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = logsFolder,
                    UseShellExecute = true
                });

                _loggingService.LogInfo("Opened logs folder");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error opening logs folder", ex);
                _dialogService.ShowMessage("Failed to open logs folder.", "Error");
            }
        }

        [RelayCommand]
        private void ExportSettings()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var fileName = $"AppIntBlocker_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _dialogService.SaveFileDialog(
                    "Export Settings",
                    "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    "json",
                    fileName);

                if (!string.IsNullOrEmpty(filePath))
                {
                    var currentSettings = new Models.AppSettings
                    {
                        CreateRestorePoint = AutoCreateRestorePoint,
                        EnableDetailedLogging = EnableDetailedLogging,
                        IncludeSubdirectories = DefaultIncludeSubdirectories,
                        BlockExeFiles = DefaultBlockExeFiles,
                        BlockDllFiles = DefaultBlockDllFiles,
                        UseExclusions = EnableExclusionsByDefault,
                        ExcludedKeywords = DefaultExcludedKeywords,
                        ExcludedFiles = DefaultExcludedFiles
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(currentSettings, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(filePath, json);

                    _dialogService.ShowMessage($"Settings exported successfully to:\n{filePath}", "Export Complete");
                    _loggingService.LogInfo($"Settings exported to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error exporting settings", ex);
                _dialogService.ShowError("Failed to export settings. Check the log for details.");
            }
        }

        [RelayCommand]
        private void ImportSettings()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var filePath = _dialogService.OpenFileDialog(
                    "Import Settings",
                    "JSON files (*.json)|*.json|All files (*.*)|*.*");

                if (!string.IsNullOrEmpty(filePath))
                {
                    var confirmMessage = "This will overwrite your current settings with the imported ones.\n\n" +
                                       "Are you sure you want to continue?";

                    if (!_dialogService.ShowConfirmation(confirmMessage, "Import Settings"))
                    {
                        return;
                    }

                    var json = File.ReadAllText(filePath);
                    var importedSettings = System.Text.Json.JsonSerializer.Deserialize<Models.AppSettings>(json);

                    if (importedSettings != null)
                    {
                        // Apply imported settings
                        AutoCreateRestorePoint = importedSettings.CreateRestorePoint;
                        EnableDetailedLogging = importedSettings.EnableDetailedLogging;
                        DefaultIncludeSubdirectories = importedSettings.IncludeSubdirectories;
                        DefaultBlockExeFiles = importedSettings.BlockExeFiles;
                        DefaultBlockDllFiles = importedSettings.BlockDllFiles;
                        EnableExclusionsByDefault = importedSettings.UseExclusions;
                        DefaultExcludedKeywords = importedSettings.ExcludedKeywords;
                        DefaultExcludedFiles = importedSettings.ExcludedFiles;

                        _dialogService.ShowMessage($"Settings imported successfully from:\n{filePath}", "Import Complete");
                        _loggingService.LogInfo($"Settings imported from: {filePath}");
                    }
                    else
                    {
                        _dialogService.ShowError("Invalid settings file format.");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error importing settings", ex);
                _dialogService.ShowError("Failed to import settings. Check the log for details.");
            }
        }
    }
} 