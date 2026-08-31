using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace cchay_optimicer_cs.Services
{
    /// <summary>
    /// Centralized utility for JSON file persistence.
    /// Replaces duplicated load/save patterns across TweakService, NetworkService, SettingsService.
    /// </summary>
    public static class JsonStorageHelper
    {
        public static string AppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RethOptimicer"
        );

        private static readonly string AppDataDir = AppDataDirectory;
        private static readonly string LegacyAppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CchayOptimicer"
        );

        public static void MigrateLegacyData()
        {
            try
            {
                if (!Directory.Exists(LegacyAppDataDir) || Directory.Exists(AppDataDir))
                    return;

                Directory.CreateDirectory(AppDataDir);
                foreach (var file in Directory.GetFiles(LegacyAppDataDir))
                {
                    string dest = Path.Combine(AppDataDir, Path.GetFileName(file));
                    if (!File.Exists(dest))
                        File.Copy(file, dest);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonStorageHelper] Legacy migration skipped: {ex.Message}");
            }
        }

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Returns the full path for a named file inside the app's LocalAppData folder.
        /// </summary>
        public static string GetFilePath(string fileName)
        {
            return Path.Combine(AppDataDir, fileName);
        }

        /// <summary>
        /// Loads and deserializes a JSON file. Returns the default value if the file
        /// doesn't exist or if deserialization fails.
        /// </summary>
        public static T Load<T>(string fileName) where T : new()
        {
            try
            {
                string path = GetFilePath(fileName);
                if (!File.Exists(path))
                {
                    string legacy = Path.Combine(LegacyAppDataDir, fileName);
                    if (File.Exists(legacy))
                        path = legacy;
                }
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<T>(json) ?? new T();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonStorageHelper] Failed to load '{fileName}': {ex.Message}");
            }
            return new T();
        }

        /// <summary>
        /// Serializes and saves an object to a JSON file in the app's LocalAppData folder.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        public static void Save<T>(string fileName, T data)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                string path = GetFilePath(fileName);
                string json = JsonSerializer.Serialize(data, WriteOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JsonStorageHelper] Failed to save '{fileName}': {ex.Message}");
            }
        }
    }
}
