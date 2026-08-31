using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class QuickOptimizeService
    {
        public static async Task RunQuickOptimizeAsync(Action<string, int> progressCallback)
        {
            // Step 1: Create System Restore Point (10%)
            progressCallback("Creando punto de restauración del sistema...", 10);
            await Task.Delay(200); // Allow UI to update
            if (SettingsService.Settings.AutoRestorePointEnabled)
            {
                try
                {
                    await BackupService.CreateRestorePointAsync("RETH Optimicer");
                }
                catch { }
            }

            // Step 2: Clean RAM fully (25%)
            progressCallback("Liberando memoria RAM (Working Set, Standby, etc.)...", 25);
            await Task.Delay(200);
            try
            {
                long freed = await RamService.CleanAll();
                SettingsService.Settings.TotalBytesRamFreed += (ulong)freed;
                SettingsService.SaveSettings();
            }
            catch { }

            // Step 3: Clean Disk Temp Files & Dev Caches (40%)
            progressCallback("Eliminando archivos temporales de disco y caché...", 40);
            await Task.Delay(200);
            try
            {
                var targetKeys = new System.Collections.Generic.HashSet<string> { "temp", "windowsTemp", "recycleBin", "thumbnails", "errorReports", "windowsLogs" };
                if (SettingsService.Settings.DevModeEnabled)
                {
                    targetKeys.Add("npmCache");
                    targetKeys.Add("nugetCache");
                    targetKeys.Add("pipCache");
                    targetKeys.Add("vscodeCache");
                }
                var targets = DiskService.GetTargets().Where(t => targetKeys.Contains(t.Key));
                double diskFreed = 0;
                foreach (var t in targets)
                {
                    try
                    {
                        var res = await DiskService.ProcessTargetAsync(t, delete: true);
                        diskFreed += res.SizeMB;
                    }
                    catch { } // Skip individual target failures
                }
                SettingsService.Settings.TotalMbDiskFreed += diskFreed;
                SettingsService.SaveSettings();
            }
            catch { }

            // Step 4: Apply a small whitelist of reversible performance tweaks.
            progressCallback("Aplicando ajustes de rendimiento seguros...", 55);
            await Task.Delay(200);
            try
            {
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ultimate-performance",
                    "disable-nagle",
                    "disable-gamebar",
                    "visual-performance"
                };
                var tweaks = await TweakService.GetTweaksAsync();
                foreach (var tweak in tweaks)
                {
                    if (!allowed.Contains(tweak.Key) || tweak.Enabled)
                        continue;
                    try
                    {
                        await TweakService.ApplyTweakAsync(tweak.Key);
                    }
                    catch { }
                }
            }
            catch { }

            // Step 5: Flush DNS only (do not disable OneDrive/Widgets from one-click).
            progressCallback("Vaciando caché DNS...", 70);
            await Task.Delay(200);
            try
            {
                await NetworkService.FlushDnsAsync();
            }
            catch { }

            // Step 6: TCP tweaks only — do not hijack the user's DNS.
            progressCallback("Aplicando ajustes TCP de baja latencia...", 85);
            await Task.Delay(200);
            try
            {
                await NetworkService.ApplyTcpTweaksAsync(true);
            }
            catch { }

            // Step 7: Finish (100%)
            progressCallback("¡Optimización del sistema finalizada con éxito!", 100);
            await Task.Delay(500); // Give user time to see the final message
        }
    }
}
