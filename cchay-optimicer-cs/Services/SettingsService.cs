using System;

namespace cchay_optimicer_cs.Services
{
    public class AppSettings
    {
        public bool AutoRestorePointEnabled { get; set; } = true;
        public bool AutoRamCleanEnabled { get; set; } = false;
        public int AutoRamCleanThreshold { get; set; } = 80;
        public ulong TotalBytesRamFreed { get; set; } = 0;
        public double TotalMbDiskFreed { get; set; } = 0;
        public int TotalOptimizationsRun { get; set; } = 0;
        public bool MinimizeToTray { get; set; } = true;
        public bool DailyMaintenanceEnabled { get; set; } = false;
        public string DailyMaintenanceTime { get; set; } = "03:00";
        public DateTime LastDailyMaintenanceDate { get; set; } = DateTime.MinValue;
        public bool GamerModeEnabled { get; set; } = false;
        public List<string> GamerProcesses { get; set; } = new List<string> { "csgo", "cs2", "valorant", "fortnite", "leagueoflegends", "gta5" };
        public bool DevModeEnabled { get; set; } = false;
        public List<string> DevProcesses { get; set; } = new List<string> { "code", "devenv", "idea64", "rider64", "androidstudio64", "dotnet", "node", "python", "git" };
        public bool WidgetEnabled { get; set; } = false;
        public bool AntiStutterEnabled { get; set; } = false;
    }

    public static class SettingsService
    {
        private static AppSettings _settings = new AppSettings();

        static SettingsService()
        {
            LoadSettings();
        }

        public static AppSettings Settings => _settings;

        public static void LoadSettings()
        {
            _settings = JsonStorageHelper.Load<AppSettings>("settings.json");
        }

        public static void SaveSettings()
        {
            JsonStorageHelper.Save("settings.json", _settings);
        }
    }
}
