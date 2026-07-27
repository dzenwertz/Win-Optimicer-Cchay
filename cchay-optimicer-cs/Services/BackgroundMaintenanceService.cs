using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace cchay_optimicer_cs.Services
{
    public class BackgroundMaintenanceService
    {
        private static System.Threading.Timer? _ramTimer;
        private static System.Threading.Timer? _dailyTimer;
        private static System.Threading.Timer? _gamerTimer;
        private static System.Threading.Timer? _devTimer;
        private static System.Threading.Timer? _stutterTimer;
        private static bool _isRamRunning = false;
        private static bool _isDailyRunning = false;

        public static void Initialize()
        {
            if (_ramTimer != null) return;

            // Timer 1: Auto RAM check every 2 minutes
            _ramTimer = new System.Threading.Timer(async _ => await CheckAndCleanRamAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));

            // Timer 2: Daily maintenance check every 15 minutes
            _dailyTimer = new System.Threading.Timer(async _ => await CheckDailyMaintenanceAsync(), null, TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(15));

            // Timer 3: Gamer process priority watcher every 15 seconds
            _gamerTimer = new System.Threading.Timer(_ => CheckGamerProcessesPriority(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));

            // Timer 4: Dev process priority watcher every 15 seconds
            _devTimer = new System.Threading.Timer(_ => CheckDevProcessesPriority(), null, TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15));

            // Timer 5: Anti-Stuttering proactive memory purge check every 30 seconds
            _stutterTimer = new System.Threading.Timer(async _ => await AntiStutterService.CheckAndPurgeIfNeededAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
        }

        private static async Task CheckAndCleanRamAsync()
        {
            if (_isRamRunning) return;

            var settings = SettingsService.Settings;
            if (!settings.AutoRamCleanEnabled) return;

            _isRamRunning = true;
            try
            {
                var mem = RamService.GetMemoryUsage();
                if (mem.PercentUsed >= settings.AutoRamCleanThreshold)
                {
                    Debug.WriteLine($"[AutoRAM] Memory usage is {mem.PercentUsed}%, which exceeds threshold {settings.AutoRamCleanThreshold}%. Cleaning RAM...");
                    long freed = await RamService.CleanAll();
                    
                    // Increment RAM freed stats
                    settings.TotalBytesRamFreed += (ulong)freed;
                    settings.TotalOptimizationsRun++;
                    SettingsService.SaveSettings();

                    Debug.WriteLine($"[AutoRAM] RAM Clean finished. Freed {freed / 1024.0 / 1024.0:F0} MB.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoRAM] Error during auto clean: {ex.Message}");
            }
            finally
            {
                _isRamRunning = false;
            }
        }

        private static async Task CheckDailyMaintenanceAsync()
        {
            if (_isDailyRunning) return;

            var settings = SettingsService.Settings;
            if (!settings.DailyMaintenanceEnabled) return;

            _isDailyRunning = true;
            try
            {
                var now = DateTime.Now;
                
                // Parse the target time (e.g. "03:00")
                if (TimeSpan.TryParse(settings.DailyMaintenanceTime, out var targetTime))
                {
                    var targetDateTime = now.Date.Add(targetTime);
                    
                    // If current time is after target time, and last maintenance wasn't today
                    if (now >= targetDateTime && settings.LastDailyMaintenanceDate.Date < now.Date)
                    {
                        Debug.WriteLine($"[DailyMaintenance] Starting daily scheduled optimization at {now}...");
                        
                        // Run technical optimize flow
                        await QuickOptimizeService.RunQuickOptimizeAsync((status, percent) => {
                            Debug.WriteLine($"[DailyMaintenance] {status} ({percent}%)");
                        });

                        settings.LastDailyMaintenanceDate = now;
                        settings.TotalOptimizationsRun++;
                        SettingsService.SaveSettings();

                        Debug.WriteLine("[DailyMaintenance] Daily scheduled optimization finished successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DailyMaintenance] Error during daily maintenance: {ex.Message}");
            }
            finally
            {
                _isDailyRunning = false;
            }
        }

        private static void CheckGamerProcessesPriority()
        {
            try
            {
                var settings = SettingsService.Settings;
                if (!settings.GamerModeEnabled || settings.GamerProcesses == null || settings.GamerProcesses.Count == 0)
                    return;

                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        if (settings.GamerProcesses.Exists(p => p.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (proc.PriorityClass != ProcessPriorityClass.High)
                            {
                                proc.PriorityClass = ProcessPriorityClass.High;
                                Debug.WriteLine($"[GamerMode] Elevated CPU priority for {proc.ProcessName} (PID: {proc.Id}) to High.");
                            }
                        }
                    }
                    catch
                    {
                        // Access denied to system process
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GamerMode] Error checking gamer processes: {ex.Message}");
            }
        }

        private static void CheckDevProcessesPriority()
        {
            try
            {
                var settings = SettingsService.Settings;
                if (!settings.DevModeEnabled || settings.DevProcesses == null || settings.DevProcesses.Count == 0)
                    return;

                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        if (settings.DevProcesses.Exists(p => p.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (proc.PriorityClass != ProcessPriorityClass.AboveNormal && proc.PriorityClass != ProcessPriorityClass.High)
                            {
                                proc.PriorityClass = ProcessPriorityClass.AboveNormal;
                                Debug.WriteLine($"[DevMode] Elevated CPU priority for {proc.ProcessName} (PID: {proc.Id}) to AboveNormal.");
                            }
                        }
                    }
                    catch
                    {
                        // Access denied to system process
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DevMode] Error checking dev processes: {ex.Message}");
            }
        }
    }
}
