using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class TweakService
    {
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CchayOptimicer",
            "tweakStates.json"
        );

        private static Dictionary<string, bool> LoadTweakStates()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
                }
            }
            catch { /* ignore */ }
            return new Dictionary<string, bool>();
        }

        private static void SaveTweakStates(Dictionary<string, bool> states)
        {
            try
            {
                string dir = Path.GetDirectoryName(StateFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save tweak states: {ex.Message}");
            }
        }

        private static void RunCmd(string filename, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var process = Process.Start(psi);
                process?.WaitForExit(3000);
            }
            catch { /* ignore */ }
        }

        public static Task<List<Tweak>> GetTweaksAsync()
        {
            return Task.Run(() =>
            {
                var states = LoadTweakStates();
                var tweaks = new List<Tweak>
                {
                    // === PERFORMANCE ===
                    new Tweak {
                        Key = "disable-superfetch",
                        Name = "Desactivar Superfetch/SysMain",
                        Description = "Desactiva el servicio SysMain que precarga apps en memoria. Recomendado para SSD.\n⚠️ Consecuencia: Las aplicaciones instaladas en discos duros convencionales (HDD) podrían iniciar un poco más despacio.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-indexing",
                        Name = "Desactivar Indexación (WSearch)",
                        Description = "Desactiva la indexación de Windows Search. Reduce uso de disco y CPU.\n⚠️ Consecuencia: Buscar archivos locales en el Explorador de Windows tardará notablemente más tiempo.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "optimize-cpu-priority",
                        Name = "Optimizar Prioridad CPU",
                        Description = "Ajusta Win32PrioritySeparation para dar más prioridad a la app activa en primer plano.\n⚠️ Consecuencia: Las tareas pesadas de segundo plano (como renderizados o descargas) recibirán menos recursos de CPU.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "reduce-app-timeout",
                        Name = "Reducir Timeout de Apps",
                        Description = "Reduce el tiempo de espera para cerrar apps colgadas. Apagado más rápido.\n⚠️ Consecuencia: Si una aplicación legítima está guardando datos pesados al apagar el equipo, podría ser cerrada de forma anticipada.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "ultimate-performance",
                        Name = "Plan Energía: Ultimate Performance",
                        Description = "Activa el plan de energía de máximo rendimiento de Windows.\n⚠️ Consecuencia: Aumentará el consumo de batería en laptops y las temperaturas generales del equipo en reposo.",
                        Category = "performance",
                        Risk = "moderate"
                    },
                    new Tweak {
                        Key = "optimize-multimedia",
                        Name = "Optimizar Perfil Multimedia",
                        Description = "Ajusta prioridades de GPU y CPU para juegos y contenido multimedia.\n⚠️ Consecuencia: Tareas de fondo no relacionadas con juegos (compiladores, servidores locales) podrían ralentizarse temporalmente.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-network-throttling",
                        Name = "Desactivar Network Throttling",
                        Description = "Elimina el límite artificial de red impuesto a actividades no multimedia.\n⚠️ Consecuencia: Durante transferencias de red muy pesadas, el sistema no autolimitará el ancho de banda, lo que podría aumentar el ping en juegos.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "optimize-ram-behavior",
                        Name = "Optimizar Comportamiento de RAM",
                        Description = "Deshabilita la paginación ejecutiva del kernel (DisablePagingExecutive) para mantener el kernel en memoria física.\n⚠️ Consecuencia: Requiere más memoria RAM física libre; no se recomienda su uso en equipos con menos de 8GB de RAM.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "network-latency-tweaks",
                        Name = "Optimizar Latencia de Red",
                        Description = "Deshabilita algoritmos de retardo de TCP/IP (Nagle/TCPNoDelay) y habilita Receive Side Scaling (RSS).\n⚠️ Consecuencia: Podría causar un consumo de CPU ligeramente más alto durante descargas a velocidades gigabit.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-text-prediction",
                        Name = "Desactivar Predicción de Texto",
                        Description = "Desactiva la predicción de texto de teclado físico y virtual de Windows.\n⚠️ Consecuencia: El teclado en pantalla y el corrector ortográfico nativo de Windows no sugerirán palabras ni corregirán automáticamente.",
                        Category = "performance",
                        Risk = "safe"
                    },
 
                    // === PRIVACY ===
                    new Tweak {
                        Key = "disable-telemetry",
                        Name = "Desactivar Telemetría",
                        Description = "Desactiva DiagTrack y servicios de recopilación de datos de Microsoft.\n⚠️ Consecuencia: Microsoft no recibirá reportes de fallos sobre el equipo, y algunas opciones de diagnóstico en Windows Update podrían aparecer deshabilitadas.",
                        Category = "privacy",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-advertising-id",
                        Name = "Desactivar Advertising ID",
                        Description = "Evita que Windows use un identificador único para rastrear tu actividad publicitaria.\n⚠️ Consecuencia: Seguirás viendo la misma cantidad de anuncios en apps, pero ya no estarán personalizados según tus búsquedas.",
                        Category = "privacy",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-cortana",
                        Name = "Desactivar Cortana y Búsqueda Web",
                        Description = "Desactiva el asistente Cortana y las búsquedas en la web desde el menú de inicio.\n⚠️ Consecuencia: Las búsquedas del menú de inicio se limitarán exclusivamente a archivos, programas y carpetas locales.",
                        Category = "privacy",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-activity-history",
                        Name = "Desactivar Historial de Actividad",
                        Description = "Impide que Windows registre y suba a tu cuenta el historial de actividad diaria.\n⚠️ Consecuencia: Se deshabilitará la línea de tiempo de Windows (Timeline) y no se sincronizarán tus últimos archivos abiertos entre dispositivos.",
                        Category = "privacy",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-copilot",
                        Name = "Desactivar Windows Copilot",
                        Description = "Deshabilita por completo la integración y botón de Copilot en la barra de tareas.\n⚠️ Consecuencia: El asistente inteligente de IA integrado en Windows 11 quedará inactivo e inaccesible.",
                        Category = "privacy",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-bing-search",
                        Name = "Desactivar Búsquedas Bing en Inicio",
                        Description = "Evita que el menú de inicio cargue resultados de Bing cuando buscas algo local.\n⚠️ Consecuencia: El menú de inicio no mostrará resultados de la web, imágenes de internet ni sugerencias climáticas en línea.",
                        Category = "privacy",
                        Risk = "safe"
                    },
 
                    // === VISUAL ===
                    new Tweak {
                        Key = "classic-context-menu",
                        Name = "Menú Contextual Clásico (Win11)",
                        Description = "Restaura el menú contextual completo clásico de Windows 10 al hacer clic derecho.\n⚠️ Consecuencia: Oculta la nueva interfaz de menú simplificada de Windows 11. Requiere reiniciar explorer.exe (se realiza automáticamente).",
                        Category = "visual",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "show-file-extensions",
                        Name = "Mostrar Extensiones de Archivo",
                        Description = "Muestra las extensiones ocultas por defecto (como .exe, .pdf, .txt) en el Explorador.\n⚠️ Consecuencia: Al renombrar archivos, deberás cuidar de no alterar la extensión manualmente o el archivo podría quedar inutilizable temporalmente.",
                        Category = "visual",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-transparency",
                        Name = "Desactivar Transparencia",
                        Description = "Desactiva los efectos acrílicos y translúcidos en las ventanas y barra de tareas.\n⚠️ Consecuencia: Los menús y bordes del sistema tendrán un aspecto de color opaco (sólido) en lugar de translúcido.",
                        Category = "visual",
                        Risk = "safe"
                    },
 
                    // === GAMING ===
                    new Tweak {
                        Key = "enable-game-mode",
                        Name = "Habilitar Game Mode + HAGS",
                        Description = "Activa el Modo de Juego y la programación de GPU acelerada por hardware.\n⚠️ Consecuencia: Podría requerir reiniciar el sistema para que HAGS tenga efecto. En tarjetas gráficas antiguas o drivers inestables, podría causar congelamientos.",
                        Category = "gaming",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-gamebar",
                        Name = "Desactivar Game Bar y DVR",
                        Description = "Desactiva Xbox Game Bar y la grabación en segundo plano de clips de juegos.\n⚠️ Consecuencia: No podrás utilizar el atajo de teclado Win+G, tomar capturas nativas de Xbox, ni grabar clips de juegos en segundo plano.",
                        Category = "gaming",
                        Risk = "safe"
                    },
 
                    // === SERVICES ===
                    new Tweak {
                        Key = "disable-error-reporting",
                        Name = "Desactivar Windows Error Reporting",
                        Description = "Desactiva el servicio de telemetría y reporte de errores de Windows.\n⚠️ Consecuencia: Si una aplicación falla, no se enviarán datos de diagnóstico a Microsoft y no se buscarán soluciones automáticas en la red.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-print-spooler",
                        Name = "Desactivar Print Spooler",
                        Description = "Desactiva el servicio Spooler encargado de gestionar colas de impresión.\n⚠️ Consecuencia: No podrás realizar impresiones en papel físico ni utilizar la impresora PDF virtual de Windows para guardar archivos.",
                        Category = "services",
                        Risk = "moderate"
                    }
                };

                foreach (var tweak in tweaks)
                {
                    tweak.Enabled = states.ContainsKey(tweak.Key) && states[tweak.Key];
                }

                return tweaks;
            });
        }

        public static Task<bool> ApplyTweakAsync(string key)
        {
            return Task.Run(() =>
            {
                try
                {
                    switch (key)
                    {
                        case "disable-superfetch":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnableSuperfetch", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 0, RegistryValueKind.DWord);
                            RunCmd("sc", "stop SysMain");
                            break;

                        case "disable-indexing":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop WSearch");
                            break;

                        case "optimize-cpu-priority":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                            break;

                        case "reduce-app-timeout":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", "1", RegistryValueKind.String);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "HungAppTimeout", "1000", RegistryValueKind.String);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WaitToKillAppTimeout", "2000", RegistryValueKind.String);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", RegistryValueKind.String);
                            break;

                        case "ultimate-performance":
                            RunCmd("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                            RunCmd("powercfg", "-setactive e9a42b02-d5df-448d-aa00-03f14749eb61");
                            break;

                        case "optimize-multimedia":
                            string mmPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                            Registry.SetValue(mmPath, "SystemResponsiveness", 1, RegistryValueKind.DWord);
                            Registry.SetValue(mmPath, "NoLazyMode", 1, RegistryValueKind.DWord);
                            Registry.SetValue(mmPath + @"\Tasks\Games", "GPU Priority", 8, RegistryValueKind.DWord);
                            Registry.SetValue(mmPath + @"\Tasks\Games", "Priority", 6, RegistryValueKind.DWord);
                            Registry.SetValue(mmPath + @"\Tasks\Games", "Scheduling Category", "High", RegistryValueKind.String);
                            Registry.SetValue(mmPath + @"\Tasks\Games", "SFIO Priority", "High", RegistryValueKind.String);
                            break;

                        case "disable-network-throttling":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-telemetry":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop DiagTrack");
                            RunCmd("sc", "stop dmwappushservice");
                            break;

                        case "disable-advertising-id":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, RegistryValueKind.DWord);
                            break;

                        case "disable-cortana":
                            string searchPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
                            Registry.SetValue(searchPath, "AllowCortana", 0, RegistryValueKind.DWord);
                            Registry.SetValue(searchPath, "DisableWebSearch", 1, RegistryValueKind.DWord);
                            Registry.SetValue(searchPath, "ConnectedSearchUseWeb", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-activity-history":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, RegistryValueKind.DWord);
                            break;

                        case "classic-context-menu":
                            string clsidPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
                            Registry.SetValue(clsidPath, "", "", RegistryValueKind.String);
                            RestartExplorer();
                            break;

                        case "show-file-extensions":
                            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-transparency":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, RegistryValueKind.DWord);
                            break;

                        case "enable-game-mode":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                            break;

                        case "disable-gamebar":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-error-reporting":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop WerSvc");
                            break;

                        case "disable-print-spooler":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop Spooler");
                            break;

                        case "optimize-ram-behavior":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, RegistryValueKind.DWord);
                            break;

                        case "network-latency-tweaks":
                            try
                            {
                                using (var interfacesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                                {
                                    if (interfacesKey != null)
                                    {
                                        foreach (var subkeyName in interfacesKey.GetSubKeyNames())
                                        {
                                            using (var ipKey = interfacesKey.OpenSubKey(subkeyName, true))
                                            {
                                                if (ipKey != null)
                                                {
                                                    ipKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                                    ipKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                            RunCmd("netsh", "int tcp set global rss=enabled");
                            RunCmd("netsh", "int tcp set global autotuninglevel=normal");
                            break;

                        case "disable-text-prediction":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7", "EnableAutoShiftEngage", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\TextInput", "EnableTextPrediction", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-copilot":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-bing-search":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0, RegistryValueKind.DWord);
                            break;

                        default:
                            return false;
                    }

                    var states = LoadTweakStates();
                    states[key] = true;
                    SaveTweakStates(states);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error applying tweak {key}: {ex.Message}");
                    return false;
                }
            });
        }

        public static Task<bool> UnapplyTweakAsync(string key)
        {
            return Task.Run(() =>
            {
                try
                {
                    switch (key)
                    {
                        case "disable-superfetch":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", "Start", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnableSuperfetch", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 1, RegistryValueKind.DWord);
                            RunCmd("sc", "start SysMain");
                            break;

                        case "disable-indexing":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch", "Start", 2, RegistryValueKind.DWord);
                            RunCmd("sc", "start WSearch");
                            break;

                        case "optimize-cpu-priority":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 2, RegistryValueKind.DWord);
                            break;

                        case "reduce-app-timeout":
                            try
                            {
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                                {
                                    k?.DeleteValue("AutoEndTasks", false);
                                    k?.DeleteValue("HungAppTimeout", false);
                                    k?.DeleteValue("WaitToKillAppTimeout", false);
                                }
                                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "5000", RegistryValueKind.String);
                            }
                            catch { }
                            break;

                        case "ultimate-performance":
                            // Set active back to Balanced (381b4222-f694-41f0-9685-ff5bb260df2e)
                            RunCmd("powercfg", "-setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
                            break;

                        case "optimize-multimedia":
                            string mmPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                            Registry.SetValue(mmPath, "SystemResponsiveness", 14, RegistryValueKind.DWord);
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true))
                                {
                                    k?.DeleteValue("NoLazyMode", false);
                                }
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true))
                                {
                                    k?.DeleteValue("GPU Priority", false);
                                    k?.DeleteValue("Priority", false);
                                    k?.DeleteValue("Scheduling Category", false);
                                    k?.DeleteValue("SFIO Priority", false);
                                }
                            }
                            catch { }
                            break;

                        case "disable-network-throttling":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true))
                                {
                                    k?.DeleteValue("NetworkThrottlingIndex", false);
                                }
                            }
                            catch { }
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 80, RegistryValueKind.DWord);
                            break;

                        case "disable-telemetry":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true))
                                {
                                    k?.DeleteValue("AllowTelemetry", false);
                                }
                            }
                            catch { }
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 2, RegistryValueKind.DWord);
                            RunCmd("sc", "start DiagTrack");
                            RunCmd("sc", "start dmwappushservice");
                            break;

                        case "disable-advertising-id":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1, RegistryValueKind.DWord);
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", true))
                                {
                                    k?.DeleteValue("DisabledByGroupPolicy", false);
                                }
                            }
                            catch { }
                            break;

                        case "disable-cortana":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true))
                                {
                                    k?.DeleteValue("AllowCortana", false);
                                    k?.DeleteValue("DisableWebSearch", false);
                                    k?.DeleteValue("ConnectedSearchUseWeb", false);
                                }
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search", true))
                                {
                                    k?.DeleteValue("BingSearchEnabled", false);
                                    k?.DeleteValue("CortanaConsent", false);
                                }
                            }
                            catch { }
                            break;

                        case "disable-activity-history":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System", true))
                                {
                                    k?.DeleteValue("PublishUserActivities", false);
                                    k?.DeleteValue("UploadUserActivities", false);
                                }
                            }
                            catch { }
                            break;

                        case "classic-context-menu":
                            try
                            {
                                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
                                RestartExplorer();
                            }
                            catch { }
                            break;

                        case "show-file-extensions":
                            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1, RegistryValueKind.DWord);
                            break;

                        case "disable-transparency":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1, RegistryValueKind.DWord);
                            break;

                        case "enable-game-mode":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 0, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehaviorMode", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-gamebar":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
                            try
                            {
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", true))
                                {
                                    k?.DeleteValue("UseNexusForGameBarEnabled", false);
                                }
                            }
                            catch { }
                            break;

                        case "disable-error-reporting":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", true))
                                {
                                    k?.DeleteValue("Disabled", false);
                                }
                            }
                            catch { }
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc", "Start", 3, RegistryValueKind.DWord);
                            RunCmd("sc", "start WerSvc");
                            break;

                        case "disable-print-spooler":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler", "Start", 2, RegistryValueKind.DWord);
                            RunCmd("sc", "start Spooler");
                            break;

                        case "optimize-ram-behavior":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", true))
                                {
                                    k?.DeleteValue("DisablePagingExecutive", false);
                                }
                            }
                            catch { }
                            break;

                        case "network-latency-tweaks":
                            try
                            {
                                using (var interfacesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                                {
                                    if (interfacesKey != null)
                                    {
                                        foreach (var subkeyName in interfacesKey.GetSubKeyNames())
                                        {
                                            using (var ipKey = interfacesKey.OpenSubKey(subkeyName, true))
                                            {
                                                if (ipKey != null)
                                                {
                                                    ipKey.DeleteValue("TcpAckFrequency", false);
                                                    ipKey.DeleteValue("TCPNoDelay", false);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                            break;

                        case "disable-text-prediction":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\TabletTip\1.7", "EnableAutoShiftEngage", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\TextInput", "EnableTextPrediction", 1, RegistryValueKind.DWord);
                            break;

                        case "disable-copilot":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", true))
                                {
                                    k?.DeleteValue("TurnOffWindowsCopilot", false);
                                }
                            }
                            catch { }
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 1, RegistryValueKind.DWord);
                            break;

                        case "disable-bing-search":
                            try
                            {
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search", true))
                                {
                                    k?.DeleteValue("BingSearchEnabled", false);
                                    k?.DeleteValue("CortanaConsent", false);
                                }
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true))
                                {
                                    k?.DeleteValue("DisableWebSearch", false);
                                    k?.DeleteValue("ConnectedSearchUseWeb", false);
                                }
                            }
                            catch { }
                            break;

                        default:
                            return false;
                    }

                    var states = LoadTweakStates();
                    states[key] = false;
                    SaveTweakStates(states);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reverting tweak {key}: {ex.Message}");
                    return false;
                }
            });
        }

        private static void RestartExplorer()
        {
            Task.Run(() =>
            {
                RunCmd("taskkill", "/f /im explorer.exe");
                // Wait for Explorer to exit
                Task.Delay(500).Wait();
                RunCmd("cmd.exe", "/c start explorer.exe");
            });
        }
    }
}
