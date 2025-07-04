using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using AppIntBlockerGUI.Models;
using Newtonsoft.Json;

namespace AppIntBlockerGUI.Services
{
    public interface ISettingsService
    {
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
        string GetSettingsFilePath();
    }

    public class SettingsService : ISettingsService
    {
        private static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppIntBlockerGUI");
        private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json.protected");
        
        // Optional: Entropy adds an extra layer of security, but makes the data non-portable
        private static readonly byte[] Entropy = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public SettingsService()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new AppSettings();
                }

                var protectedData = File.ReadAllBytes(SettingsFilePath);
                var unprotectedData = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(unprotectedData);

                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., file corruption, permission issues)
                // For simplicity, we return default settings. A real app might log this.
                Console.WriteLine($"Failed to load settings: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., permission issues)
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public string GetSettingsFilePath() => SettingsFilePath;
    }
} 