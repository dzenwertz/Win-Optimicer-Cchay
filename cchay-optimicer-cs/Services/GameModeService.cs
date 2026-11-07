using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace cchay_optimicer_cs.Services
{
    public class GameModeState
    {
        public bool IsActive { get; set; } = false;
        public DateTime? ActivatedAt { get; set; }

        // Saved original system settings for lossless restoration
        public string? OriginalPowerPlanGuid { get; set; }
        public int? OriginalWin32PrioritySeparation { get; set; }
        public int? OriginalSystemResponsiveness { get; set; }
        public int? OriginalNetworkThrottlingIndex { get; set; }
        public int? OriginalGameDvrEnabled { get; set; }
        public int? OriginalAppCaptureEnabled { get; set; }
        public bool WSearchWasRunning { get; set; } = false;
        public bool SysMainWasRunning { get; set; } = false;

        // Configurable options for Game Mode
        public bool OptimizePowerPlan { get; set; } = true;
        public bool OptimizeCpuScheduling { get; set; } = false;
        public bool OptimizeMultimediaPriority { get; set; } = false;
        public bool PauseSearchIndexing { get; set; } = false;
        public bool PauseSysMain { get; set; } = false;
        public bool DisableGameDvr { get; set; } = true;
        public bool CleanStandbyRamOnStart { get; set; } = false;
        public bool ElevateGameProcessPriority { get; set; } = true;
        public int SnapshotVersion { get; set; }
        public List<GameRegistrySnapshot> RegistrySnapshots { get; set; } = new();
    }

    public class GameRegistrySnapshot
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public int? Number { get; set; }
        public string? Text { get; set; }
        public bool Existed { get; set; }
    }

    public class GameModeOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> AppliedOptimizations { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public static class GameModeService
    {
        private const string StateFileName = "gameModeState.json";
        private static GameModeState _state = new();
        private static System.Threading.Timer? _processPriorityTimer;
        private static readonly Dictionary<int, (DateTime Started, ProcessPriorityClass Priority)> _elevatedProcessIds = new();
        private static readonly SemaphoreSlim OperationGate = new(1, 1);
        private static readonly object PriorityGate = new();

        public static event Action<bool>? StateChanged;
        public static event Action<string>? StatusUpdated;

        public static GameModeState State => _state;
        public static bool IsActive => _state.IsActive;

        // Known game executables to automatically prioritize
        private static readonly HashSet<string> KnownGameExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "csgo", "cs2", "valorant", "valorant-win64-shipping", "fortniteclient-win64-shipping",
            "leagueoflegends", "leagueclient", "gta5", "gta_sa", "cyberpunk2077", "dota2",
            "overwatch", "r5apex", "pubg", "tslgame", "rainbowsix", "rocketleague",
            "minecraft.windows", "javaw", "robloxplayerbeta", "fifa23", "fc24", "eldenring",
            "forzahorizon5", "witcher3", "destiny2", "warframe.x64", "starfield", "genshinimpact",
            "honkaistarrail", "deadbydaylight-win64-shipping", "cod", "modernwarfare"
        };

        public static void Initialize()
        {
            try
            {
                _state = JsonStorageHelper.Load<GameModeState>(StateFileName);

                if (_state.IsActive && _state.ElevateGameProcessPriority)
                {
                    // Game mode was active when app closed. Restart watcher timer.
                    StartPriorityWatcher();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameModeService] Initialize error: {ex.Message}");
                _state = new GameModeState();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                JsonStorageHelper.Save(StateFileName, _state);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameModeService] Failed to save state: {ex.Message}");
            }
        }

        /// <summary>
        /// Activates Game Mode: captures current system state, applies gaming performance adjustments,
        /// performs a safe standby memory purge, and starts the priority monitor.
        /// </summary>
        private static Task<GameModeOperationResult> ActivateCoreAsync()
        {
            return Task.Run(async () =>
            {
                var result = new GameModeOperationResult();

                if (_state.IsActive)
                {
                    result.Success = true;
                    result.Message = "El Modo Game ya se encuentra activo.";
                    return result;
                }

                StatusUpdated?.Invoke("Guardando estado previo del sistema...");
                _state.RegistrySnapshots.Clear();
                _state.SnapshotVersion = 2;
                _state.OriginalPowerPlanGuid = null;
                _state.WSearchWasRunning = false;
                _state.SysMainWasRunning = false;

                // 1. Capture current Power Scheme
                try
                {
                    if (_state.OptimizePowerPlan)
                    {
                        var power = ProcessRunner.Run("powercfg", "/getactivescheme");
                        var match = System.Text.RegularExpressions.Regex.Match(power.StdOut,
                            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                        if (!power.Success || !match.Success)
                            throw new InvalidOperationException("No se pudo guardar el plan de energía original.");
                        _state.OriginalPowerPlanGuid = match.Value;
                        SaveRecoveryState();
                        bool powerSuccess = PowerSchemeHelper.SetActiveScheme(PowerSchemeHelper.HighPerformanceGuid);
                        if (!powerSuccess) result.Errors.Add("Windows rechazó el cambio de plan de energía.");
                        if (powerSuccess)
                        {
                            result.AppliedOptimizations.Add("Plan de energía: Alto Rendimiento activado");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error al ajustar energía: {ex.Message}");
                }

                // 2. CPU Quantum / Win32PrioritySeparation
                if (_state.OptimizeCpuScheduling)
                {
                    try
                    {
                        const string prioKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl";
                        object? curPrio = Registry.GetValue(prioKey, "Win32PrioritySeparation", 2);
                        if (curPrio is int intVal)
                        {
                            _state.OriginalWin32PrioritySeparation = intVal;
                        }

                        // 38 (0x26): short variable quantum, 3:1 foreground priority boost
                        SetRegistryValue(prioKey, "Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                        result.AppliedOptimizations.Add("Prioridad de CPU: Quantum optimizado para juegos en primer plano (0x26)");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error al optimizar prioridad de CPU: {ex.Message}");
                    }
                }

                // 3. Multimedia Class Scheduler & Network Throttling
                if (_state.OptimizeMultimediaPriority)
                {
                    try
                    {
                        const string mmPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                        object? curResp = Registry.GetValue(mmPath, "SystemResponsiveness", 20);
                        if (curResp is int respVal)
                        {
                            _state.OriginalSystemResponsiveness = respVal;
                        }

                        object? curNet = Registry.GetValue(mmPath, "NetworkThrottlingIndex", 10);
                        if (curNet is int netVal)
                        {
                            _state.OriginalNetworkThrottlingIndex = netVal;
                        }

                        SetRegistryValue(mmPath, "SystemResponsiveness", 10, RegistryValueKind.DWord);
                        SetRegistryValue(mmPath, "NetworkThrottlingIndex", -1, RegistryValueKind.DWord);

                        // Configure Tasks\Games
                        string gamesPath = mmPath + @"\Tasks\Games";
                        SetRegistryValue(gamesPath, "Priority", 6, RegistryValueKind.DWord);
                        SetRegistryValue(gamesPath, "Scheduling Category", "High", RegistryValueKind.String);
                        SetRegistryValue(gamesPath, "SFIO Priority", "High", RegistryValueKind.String);

                        result.AppliedOptimizations.Add("Programador multimedia y red: Prioridad de GPU/CPU para juegos y throttling deshabilitado");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error en programador multimedia: {ex.Message}");
                    }
                }

                // 4. Pause Windows Search Indexing
                if (_state.PauseSearchIndexing)
                {
                    try
                    {
                        _state.WSearchWasRunning = IsServiceRunning("WSearch");
                        if (_state.WSearchWasRunning)
                        {
                            SaveRecoveryState();
                            RunServiceCommand("stop", "WSearch");
                            result.AppliedOptimizations.Add("Indexación de Windows Search pausada temporalmente para evitar picos de disco");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error al pausar WSearch: {ex.Message}");
                    }
                }

                // 5. Pause SysMain (Superfetch)
                if (_state.PauseSysMain)
                {
                    try
                    {
                        _state.SysMainWasRunning = IsServiceRunning("SysMain");
                        if (_state.SysMainWasRunning)
                        {
                            SaveRecoveryState();
                            RunServiceCommand("stop", "SysMain");
                            result.AppliedOptimizations.Add("SysMain pausado para evitar compresión de memoria durante partidas");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error al pausar SysMain: {ex.Message}");
                    }
                }

                // 6. Disable GameDVR background screen capture
                if (_state.DisableGameDvr)
                {
                    try
                    {
                        object? appCapture = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1);
                        if (appCapture is int acVal) _state.OriginalAppCaptureEnabled = acVal;

                        object? gameDvr = Registry.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1);
                        if (gameDvr is int gdVal) _state.OriginalGameDvrEnabled = gdVal;

                        SetRegistryValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                        SetRegistryValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                        result.AppliedOptimizations.Add("GameDVR en segundo plano desactivado (evita consumo residual de GPU)");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error al configurar GameDVR: {ex.Message}");
                    }
                }

                // 7. Safe Standby RAM flush
                if (_state.CleanStandbyRamOnStart)
                {
                    try
                    {
                        StatusUpdated?.Invoke("Purgando lista de espera (Standby RAM)...");
                        long freed = 0;
                        freed += await RamService.CleanStandbyListLowPriority();
                        freed += await RamService.CleanStandbyList();
                        result.AppliedOptimizations.Add($"Standby RAM purgada de forma segura ({freed / 1024.0 / 1024.0:F0} MB libres para el juego)");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error al purgar standby RAM: {ex.Message}");
                    }
                }

                // 8. Start Priority Watcher
                if (_state.ElevateGameProcessPriority)
                {
                    StartPriorityWatcher();
                    result.AppliedOptimizations.Add("Monitor de prioridad de procesos activo para juegos detectados");
                }

                _state.IsActive = true;
                _state.ActivatedAt = DateTime.Now;
                SaveConfig();

                result.Success = result.Errors.Count == 0;
                result.Message = result.Success ? "Modo Game activado. El rendimiento depende del juego y del hardware."
                    : "Modo Game activado parcialmente: " + string.Join(" | ", result.Errors);
                StatusUpdated?.Invoke(result.Message);
                StateChanged?.Invoke(true);

                return result;
            });
        }

        /// <summary>
        /// Deactivates Game Mode and faithfully restores all previous system parameters.
        /// </summary>
        private static Task<GameModeOperationResult> DeactivateCoreAsync()
        {
            return Task.Run(() =>
            {
                var result = new GameModeOperationResult();

                if (!_state.IsActive)
                {
                    result.Success = true;
                    result.Message = "El Modo Game ya se encuentra inactivo.";
                    return result;
                }

                StatusUpdated?.Invoke("Restaurando configuración original de Windows...");

                // 1. Stop Priority Watcher and reset elevated process priorities
                lock (PriorityGate)
                {
                    StopPriorityWatcher();
                    ResetElevatedProcesses();
                }

                // 2. Restore Power Scheme
                try
                {
                    if (!string.IsNullOrWhiteSpace(_state.OriginalPowerPlanGuid))
                    {
                    string targetGuid = !string.IsNullOrWhiteSpace(_state.OriginalPowerPlanGuid)
                        ? _state.OriginalPowerPlanGuid
                        : PowerSchemeHelper.BalancedGuid;

                    if (!PowerSchemeHelper.SetActiveScheme(targetGuid)) throw new InvalidOperationException("No se pudo restaurar el plan de energía.");
                    _state.OriginalPowerPlanGuid = null;
                    result.AppliedOptimizations.Add($"Plan de energía restaurado al original ({PowerSchemeHelper.GetActiveSchemeName()})");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error restaurando plan de energía: {ex.Message}");
                }

                foreach (var snapshot in _state.RegistrySnapshots.ToArray())
                {
                    try
                    {
                        RestoreRegistryValue(snapshot);
                        _state.RegistrySnapshots.Remove(snapshot);
                        SaveConfig();
                    }
                    catch (Exception ex) { result.Errors.Add($"Error restaurando {snapshot.Name}: {ex.Message}"); }
                }
                if (_state.SnapshotVersion < 2) RestoreLegacyValues(result);
                // 5. Restore WSearch if it was running
                if (_state.WSearchWasRunning)
                {
                    try
                    {
                        RunServiceCommand("start", "WSearch");
                        _state.WSearchWasRunning = false;
                        result.AppliedOptimizations.Add("Servicio de Windows Search reanudado");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error reanudando WSearch: {ex.Message}");
                    }
                }

                // 6. Restore SysMain if it was running
                if (_state.SysMainWasRunning)
                {
                    try
                    {
                        RunServiceCommand("start", "SysMain");
                        _state.SysMainWasRunning = false;
                        result.AppliedOptimizations.Add("Servicio SysMain reanudado");
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error reanudando SysMain: {ex.Message}");
                    }
                }

                _state.IsActive = result.Errors.Count > 0;
                _state.ActivatedAt = null;
                SaveConfig();

                result.Success = result.Errors.Count == 0;
                result.Message = result.Success ? "Modo Game desactivado. Ajustes originales restaurados."
                    : "Restauración incompleta; vuelve a desactivar para reintentar. " + string.Join(" | ", result.Errors);
                StatusUpdated?.Invoke(result.Message);
                StateChanged?.Invoke(_state.IsActive);

                return result;
            });
        }

        public static async Task<GameModeOperationResult> ActivateAsync()
        {
            await OperationGate.WaitAsync();
            try { return await ActivateCoreAsync(); }
            finally { OperationGate.Release(); }
        }

        public static async Task<GameModeOperationResult> DeactivateAsync()
        {
            await OperationGate.WaitAsync();
            try { return await DeactivateCoreAsync(); }
            finally { OperationGate.Release(); }
        }

        // Write the recovery journal before changing Windows; failure aborts that change.
        private static void SaveRecoveryState()
        {
            _state.IsActive = true;
            Directory.CreateDirectory(JsonStorageHelper.AppDataDirectory);
            string path = JsonStorageHelper.GetFilePath(StateFileName);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_state));
            File.Move(temporary, path, true);
        }

        private static void SetRegistryValue(string path, string name, object value, RegistryValueKind kind)
        {
            object? original = Registry.GetValue(path, name, null);
            if (original != null)
            {
                int separator = path.IndexOf('\\');
                var hive = path.StartsWith("HKEY_CURRENT_USER", StringComparison.Ordinal) ? Registry.CurrentUser : Registry.LocalMachine;
                using var key = hive.OpenSubKey(path.Substring(separator + 1));
                if (key == null || key.GetValueKind(name) != kind)
                    throw new InvalidOperationException($"Tipo original distinto al esperado: {name}");
            }
            if (original != null && original is not int && original is not string)
                throw new InvalidOperationException($"Tipo de registro no compatible: {name}");
            _state.RegistrySnapshots.Add(new GameRegistrySnapshot
            {
                Path = path, Name = name, Existed = original != null,
                Number = original as int?, Text = original as string
            });
            SaveRecoveryState();
            Registry.SetValue(path, name, value, kind);
        }

        private static void RestoreRegistryValue(GameRegistrySnapshot snapshot)
        {
            if (snapshot.Existed)
                Registry.SetValue(snapshot.Path, snapshot.Name,
                    snapshot.Number.HasValue ? (object)snapshot.Number.Value : snapshot.Text!,
                    snapshot.Number.HasValue ? RegistryValueKind.DWord : RegistryValueKind.String);
            else
            {
                int separator = snapshot.Path.IndexOf('\\');
                var hive = snapshot.Path.StartsWith("HKEY_CURRENT_USER", StringComparison.Ordinal)
                    ? Registry.CurrentUser : Registry.LocalMachine;
                using var key = hive.OpenSubKey(snapshot.Path.Substring(separator + 1), true);
                key?.DeleteValue(snapshot.Name, false);
            }
        }

        private static void RestoreLegacyValues(GameModeOperationResult result)
        {
            var values = new[]
            {
                (@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", _state.OriginalWin32PrioritySeparation),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", _state.OriginalSystemResponsiveness),
                (@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", _state.OriginalNetworkThrottlingIndex),
                (@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", _state.OriginalAppCaptureEnabled),
                (@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", _state.OriginalGameDvrEnabled)
            };
            foreach (var (path, name, number) in values)
            {
                if (!number.HasValue) continue;
                try { Registry.SetValue(path, name, number.Value, RegistryValueKind.DWord); }
                catch (Exception ex) { result.Errors.Add($"Error restaurando {name}: {ex.Message}"); }
            }
            result.AppliedOptimizations.Add("Sesión antigua: Tasks\\Games no tenía copia original y se conserva sin inventar valores.");
        }

        private static void RunServiceCommand(string command, string service)
        {
            var result = ProcessRunner.Run("sc.exe", $"{command} {service}");
            if (!result.Success) throw new InvalidOperationException($"{service}: {result.StdErr} {result.StdOut}");
        }

        private static bool IsServiceRunning(string serviceName)
        {
            try
            {
                var res = ProcessRunner.Run("sc", $"query {serviceName}");
                return res.Success && res.StdOut.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void StartPriorityWatcher()
        {
            if (_processPriorityTimer != null) return;

            _processPriorityTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    lock (PriorityGate) { CheckAndPrioritizeGames(); }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GameModeService] Priority watcher tick error: {ex.Message}");
                }
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        }

        private static void StopPriorityWatcher()
        {
            try
            {
                _processPriorityTimer?.Dispose();
                _processPriorityTimer = null;
            }
            catch { }
        }

        private static void CheckAndPrioritizeGames()
        {
            if (!_state.IsActive || _processPriorityTimer == null) return;

            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (KnownGameExecutables.Contains(p.ProcessName))
                    {
                        if (p.PriorityClass == ProcessPriorityClass.Normal || p.PriorityClass == ProcessPriorityClass.BelowNormal || p.PriorityClass == ProcessPriorityClass.Idle)
                        {
                            var original = p.PriorityClass;
                            var started = p.StartTime;
                            p.PriorityClass = ProcessPriorityClass.AboveNormal;
                            lock (_elevatedProcessIds)
                            {
                                _elevatedProcessIds.TryAdd(p.Id, (started, original));
                            }
                            Debug.WriteLine($"[GameModeService] Prioritized game process: {p.ProcessName} (PID: {p.Id}) to AboveNormal");
                        }
                    }
                }
                catch
                {
                    // Access denied or process exited
                }
                finally
                {
                    p.Dispose();
                }
            }
        }

        private static void ResetElevatedProcesses()
        {
            lock (_elevatedProcessIds)
            {
                foreach (var entry in _elevatedProcessIds)
                {
                    try
                    {
                        using var p = Process.GetProcessById(entry.Key);
                        if (!p.HasExited && p.StartTime == entry.Value.Started && p.PriorityClass == ProcessPriorityClass.AboveNormal)
                        {
                            p.PriorityClass = entry.Value.Priority;
                        }
                    }
                    catch { }
                }
                _elevatedProcessIds.Clear();
            }
        }

        /// <summary>
        /// Checks if any recognized game process is currently running on the PC.
        /// </summary>
        public static (bool IsRunning, string GameName, int Pid) GetActiveGameStatus()
        {
            try
            {
                var processes = Process.GetProcesses();
                try
                {
                foreach (var p in processes)
                {
                    try
                    {
                        if (KnownGameExecutables.Contains(p.ProcessName))
                        {
                            return (true, p.ProcessName, p.Id);
                        }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
                }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            catch { }

            return (false, string.Empty, 0);
        }
    }
}
