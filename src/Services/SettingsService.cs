using Newtonsoft.Json;
using AppIntBlockerGUI.Models;
using System;
using System.IO;

namespace AppIntBlockerGUI.Services
{
    public interface ISettingsService
    {
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
        string SettingsFilePath { get; }
    }

    public class SettingsService : ISettingsService
    {
        private readonly ILoggingService _logger;
        public string SettingsFilePath { get; }

        public SettingsService(ILoggingService? logger = null)
        {
            _logger = logger ?? new LoggingService();
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appDataPath, "AppIntBlockerGUI");
            Directory.CreateDirectory(appDir);
            SettingsFilePath = Path.Combine(appDir, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new AppSettings(); // Return default settings
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load settings from {SettingsFilePath}: {ex.Message}");
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save settings to {SettingsFilePath}: {ex.Message}");
            }
        }
    }
} 