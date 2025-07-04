// <copyright file="SettingsViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService settingsService;
        private readonly IDialogService dialogService;
        private readonly ILoggingService loggingService;

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
        private string backupLocation = string.Empty;

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
        private string settingsLocation = string.Empty;

        public SettingsViewModel()
        {
            this.settingsService = new SettingsService();
            this.dialogService = new DialogService();
            this.loggingService = new LoggingService();

            this.InitializeApplicationInfo();
            this.LoadSettings();
        }

        private void InitializeApplicationInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                this.AppVersion = version?.ToString() ?? "1.0.0";

                var buildDate = File.GetCreationTime(assembly.Location);
                this.BuildDate = buildDate.ToString("yyyy-MM-dd");

                this.SettingsLocation = this.settingsService.GetSettingsFilePath();

                // Set default backup location
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                this.BackupLocation = Path.Combine(appDataPath, "AppIntBlocker", "Backups");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error initializing application info", ex);
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = this.settingsService.LoadSettings();

                // Load general settings
                this.AutoCreateRestorePoint = settings.CreateRestorePoint;
                this.EnableDetailedLogging = settings.EnableDetailedLogging;
                this.DefaultIncludeSubdirectories = settings.IncludeSubdirectories;
                this.DefaultBlockExeFiles = settings.BlockExeFiles;
                this.DefaultBlockDllFiles = settings.BlockDllFiles;
                this.EnableExclusionsByDefault = settings.UseExclusions;
                this.DefaultExcludedKeywords = settings.ExcludedKeywords;
                this.DefaultExcludedFiles = settings.ExcludedFiles;

                this.loggingService.LogInfo("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error loading settings", ex);
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                var settings = new Models.AppSettings
                {
                    CreateRestorePoint = this.AutoCreateRestorePoint,
                    EnableDetailedLogging = this.EnableDetailedLogging,
                    IncludeSubdirectories = this.DefaultIncludeSubdirectories,
                    BlockExeFiles = this.DefaultBlockExeFiles,
                    BlockDllFiles = this.DefaultBlockDllFiles,
                    UseExclusions = this.EnableExclusionsByDefault,
                    ExcludedKeywords = this.DefaultExcludedKeywords,
                    ExcludedFiles = this.DefaultExcludedFiles
                };

                this.settingsService.SaveSettings(settings);

                this.dialogService.ShowMessage("Settings saved successfully!", "Settings Saved");
                this.loggingService.LogInfo("Settings saved successfully");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error saving settings", ex);
                this.dialogService.ShowMessage("Failed to save settings. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void ResetSettings()
        {
            try
            {
                var confirmMessage = "This will reset all settings to their default values.\n\n" +
                                   "Are you sure you want to continue?";

                if (!this.dialogService.ShowConfirmation(confirmMessage, "Reset Settings"))
                {
                    return;
                }

                // Reset to default values
                this.AutoCreateRestorePoint = true;
                this.EnableDetailedLogging = true;
                this.ShowConfirmationDialogs = true;
                this.AutoRefreshStatistics = true;
                this.DefaultIncludeSubdirectories = true;
                this.DefaultBlockExeFiles = true;
                this.DefaultBlockDllFiles = false;
                this.StartMinimized = false;

                this.StatisticsRefreshInterval = 30;
                this.LogCleanupDays = 30;
                this.PreferPowerShell = true;
                this.CacheFirewallRules = true;
                this.RunInBackground = true;
                this.LimitLogOutput = true;

                this.EnableExclusionsByDefault = true;
                this.DefaultExcludedKeywords = "unins, setup, install, update, temp, cache";
                this.DefaultExcludedFiles = "uninstall.exe, setup.exe, installer.exe";

                this.CustomRulePrefix = "AppBlocker Rule";
                this.DebugMode = false;
                this.ExportSettingsOnExit = false;
                this.AutoBackup = false;
                this.CheckForUpdates = true;

                this.dialogService.ShowMessage("Settings have been reset to default values.", "Settings Reset");
                this.loggingService.LogInfo("Settings reset to default values");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error resetting settings", ex);
                this.dialogService.ShowMessage("Failed to reset settings. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void BrowseBackupLocation()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var selectedPath = this.dialogService.OpenFolderDialog();

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    this.BackupLocation = selectedPath;
                    this.loggingService.LogInfo($"Backup location changed to: {this.BackupLocation}");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error browsing for backup location", ex);
                this.dialogService.ShowError("Failed to browse for backup location.");
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

                this.loggingService.LogInfo("Opened logs folder");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error opening logs folder", ex);
                this.dialogService.ShowMessage("Failed to open logs folder.", "Error");
            }
        }

        [RelayCommand]
        private void ExportSettings()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var fileName = $"AppIntBlocker_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = this.dialogService.SaveFileDialog(
                    "Export Settings",
                    "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    "json",
                    fileName);

                if (!string.IsNullOrEmpty(filePath))
                {
                    var currentSettings = new Models.AppSettings
                    {
                        CreateRestorePoint = this.AutoCreateRestorePoint,
                        EnableDetailedLogging = this.EnableDetailedLogging,
                        IncludeSubdirectories = this.DefaultIncludeSubdirectories,
                        BlockExeFiles = this.DefaultBlockExeFiles,
                        BlockDllFiles = this.DefaultBlockDllFiles,
                        UseExclusions = this.EnableExclusionsByDefault,
                        ExcludedKeywords = this.DefaultExcludedKeywords,
                        ExcludedFiles = this.DefaultExcludedFiles
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(currentSettings, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(filePath, json);

                    this.dialogService.ShowMessage($"Settings exported successfully to:\n{filePath}", "Export Complete");
                    this.loggingService.LogInfo($"Settings exported to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error exporting settings", ex);
                this.dialogService.ShowError("Failed to export settings. Check the log for details.");
            }
        }

        [RelayCommand]
        private void ImportSettings()
        {
            try
            {
                // FIXED: Use WPF dialog service instead of Windows Forms
                var filePath = this.dialogService.OpenFileDialog(
                    "Import Settings",
                    "JSON files (*.json)|*.json|All files (*.*)|*.*");

                if (!string.IsNullOrEmpty(filePath))
                {
                    var confirmMessage = "This will overwrite your current settings with the imported ones.\n\n" +
                                       "Are you sure you want to continue?";

                    if (!this.dialogService.ShowConfirmation(confirmMessage, "Import Settings"))
                    {
                        return;
                    }

                    var json = File.ReadAllText(filePath);
                    var importedSettings = System.Text.Json.JsonSerializer.Deserialize<Models.AppSettings>(json);

                    if (importedSettings != null)
                    {
                        // Apply imported settings
                        this.AutoCreateRestorePoint = importedSettings.CreateRestorePoint;
                        this.EnableDetailedLogging = importedSettings.EnableDetailedLogging;
                        this.DefaultIncludeSubdirectories = importedSettings.IncludeSubdirectories;
                        this.DefaultBlockExeFiles = importedSettings.BlockExeFiles;
                        this.DefaultBlockDllFiles = importedSettings.BlockDllFiles;
                        this.EnableExclusionsByDefault = importedSettings.UseExclusions;
                        this.DefaultExcludedKeywords = importedSettings.ExcludedKeywords;
                        this.DefaultExcludedFiles = importedSettings.ExcludedFiles;

                        this.dialogService.ShowMessage($"Settings imported successfully from:\n{filePath}", "Import Complete");
                        this.loggingService.LogInfo($"Settings imported from: {filePath}");
                    }
                    else
                    {
                        this.dialogService.ShowError("Invalid settings file format.");
                    }
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error importing settings", ex);
                this.dialogService.ShowError("Failed to import settings. Check the log for details.");
            }
        }
    }
}
