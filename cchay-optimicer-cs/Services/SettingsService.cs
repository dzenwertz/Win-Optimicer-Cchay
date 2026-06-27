using System;
using System.IO;
using System.Text.Json;

namespace cchay_optimicer_cs.Services
{
    public class AppSettings
    {
        public bool AutoRestorePointEnabled { get; set; } = true;
    }

    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CchayOptimicer",
            "settings.json"
        );

        private static AppSettings _settings = new AppSettings();

        static SettingsService()
        {
            LoadSettings();
        }

        public static AppSettings Settings => _settings;

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsFilePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
