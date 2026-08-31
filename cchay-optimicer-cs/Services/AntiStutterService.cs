using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class AntiStutterService
    {
        private static DateTime _lastPurgeTime = DateTime.MinValue;
        private static readonly TimeSpan PurgeCooldown = TimeSpan.FromSeconds(60);

        // List of target heavy process executables (games, 3D tools, IDEs, video editors)
        private static readonly HashSet<string> HeavyProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "csgo", "cs2", "valorant", "fortnite", "leagueoflegends", "gta5", "cyberpunk2077", "dota2", "overwatch",
            "photoshop", "premiere", "afterfx", "blender", "autocad", "revit", "matlab",
            "devenv", "code", "idea64", "androidstudio64", "rider64", "unity", "unrealeditor"
        };

        /// <summary>
        /// Checks if a heavy process is running and performs a safe memory purge.
        /// Only cleans standby list memory (cached/unused pages), NOT active working sets.
        /// This avoids causing page faults in foreground applications.
        /// </summary>
        public static async Task<bool> CheckAndPurgeIfNeededAsync()
        {
            var settings = SettingsService.Settings;
            if (!settings.AntiStutterEnabled) return false;

            if (DateTime.Now - _lastPurgeTime < PurgeCooldown)
                return false;

            bool detectedHeavyProcess = false;
            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (HeavyProcesses.Contains(p.ProcessName))
                        {
                            detectedHeavyProcess = true;
                            break;
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch
            {
                return false;
            }

            if (detectedHeavyProcess)
            {
                _lastPurgeTime = DateTime.Now;
                Debug.WriteLine("[AntiStutter] Heavy process detected. Executing safe standby memory purge (not touching working sets)...");
                try
                {
                    // Only purge standby list and low-priority standby pages.
                    // This frees cached/unused memory without disrupting active process working sets.
                    long freed = 0;
                    freed += await RamService.CleanStandbyListLowPriority();
                    freed += await RamService.CleanStandbyList();
                    Debug.WriteLine($"[AntiStutter] Safe purge completed. Freed {freed / 1024.0 / 1024.0:F1} MB of standby memory.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AntiStutter] Error during purge: {ex.Message}");
                }
            }

            return false;
        }

        public static bool IsCooldownActive()
        {
            return DateTime.Now - _lastPurgeTime < PurgeCooldown;
        }
    }
}
