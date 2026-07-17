using System;
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
                    await BackupService.CreateRestorePointAsync("Cchay Tech Optimize");
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

            // Step 3: Clean Disk Temp Files (40%)
            progressCallback("Eliminando archivos temporales de disco y caché...", 40);
            await Task.Delay(200);
            try
            {
                var targets = DiskService.GetTargets().Where(t => t.Key == "temp" || t.Key == "windowsTemp" || t.Key == "recycleBin" || t.Key == "thumbnails" || t.Key == "errorReports" || t.Key == "windowsLogs");
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

            // Step 4: Apply Performance Tweaks (55%)
            progressCallback("Aplicando tweaks de registro y rendimiento seguros...", 55);
            await Task.Delay(200);
            try
            {
                var tweaks = await TweakService.GetTweaksAsync();
                foreach (var tweak in tweaks)
                {
                    if (tweak.Risk == "safe" && !tweak.Enabled)
                    {
                        try
                        {
                            await TweakService.ApplyTweakAsync(tweak.Key);
                        }
                        catch { } // Skip individual tweak failures
                    }
                }
            }
            catch { }

            // Step 5: Disable widgets & OneDrive (70%)
            progressCallback("Desactivando componentes de bloatware (Widgets y OneDrive)...", 70);
            await Task.Delay(200);
            try
            {
                await BloatwareService.DisableOneDriveAsync();
            }
            catch { }
            try
            {
                await BloatwareService.DisableWidgetsAsync();
            }
            catch { }

            // Step 6: Optimize Network/DNS (85%)
            progressCallback("Analizando latencia DNS y configurando servidor más rápido...", 85);
            await Task.Delay(200);
            try
            {
                var dnsProviders = await NetworkService.TestAllPingsAsync();
                var fastest = dnsProviders.FirstOrDefault();
                if (fastest != null && fastest.Ping != 999)
                {
                    await NetworkService.SetDnsAsync(new[] { fastest.Primary, fastest.Secondary });
                }
                await NetworkService.FlushDnsAsync();
                await NetworkService.ApplyTcpTweaksAsync(true);
            }
            catch { }

            // Step 7: Finish (100%)
            progressCallback("¡Optimización del sistema finalizada con éxito!", 100);
            await Task.Delay(500); // Give user time to see the final message
        }
    }
}
