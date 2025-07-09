// <copyright file="SettingsService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using AppIntBlockerGUI.Models;
    using Newtonsoft.Json;

    public class SettingsService : ISettingsService
    {
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppIntBlockerGUI");
        private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json.protected");

        // Using a fixed entropy makes the encrypted file non-portable across machines or user accounts.
        // This is a security feature. Removing it would make it portable but less secure.
        private static readonly byte[] Entropy = { 16, 23, 42, 8, 4, 15 };

        private readonly ILoggingService loggingService;
        private readonly IDialogService dialogService;

        public SettingsService(ILoggingService loggingService, IDialogService dialogService)
        {
            this.loggingService = loggingService;
            this.dialogService = dialogService;

            try
            {
                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                    this.loggingService.LogInfo($"Settings directory created at: {AppDataPath}");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogCritical("Could not create settings directory. Application may fail to save settings.", ex);
            }
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                this.loggingService.LogInfo("Settings file not found. Returning default settings.");
                return new AppSettings();
            }

            try
            {
                var protectedData = File.ReadAllBytes(SettingsFilePath);
                var unprotectedData = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(unprotectedData);

                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Failed to load or decrypt settings file. It might be corrupted or from a different user/machine.", ex);
                
                // Backup the corrupted file and inform the user
                var backupPath = $"{SettingsFilePath}.{DateTime.Now:yyyyMMddHHmmss}.bak";
                try
                {
                    File.Move(SettingsFilePath, backupPath);
                    this.loggingService.LogInfo($"Backed up corrupted settings file to: {backupPath}");
                    this.dialogService.ShowMessage("Settings File Corrupted", $"Your settings file could not be read. A backup has been created at:\n{backupPath}\n\nThe application will start with default settings.");
                }
                catch (Exception backupEx)
                {
                     this.loggingService.LogError("Failed to backup corrupted settings file.", backupEx);
                     this.dialogService.ShowMessage("Critical Settings Error", "Your settings file is corrupted and could not be backed up. Please manually check the AppData folder. The application will use default settings.");
                }
                
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                var data = Encoding.UTF8.GetBytes(json);
                var protectedData = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(SettingsFilePath, protectedData);
                this.loggingService.LogInfo("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Failed to save settings.", ex);
                this.dialogService.ShowMessage("Error Saving Settings", "Could not save your settings. Please check file permissions for the application data folder.");
            }
        }

        public string GetSettingsFilePath() => SettingsFilePath;
    }
}
