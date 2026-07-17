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
                    },
                    new Tweak {
                        Key = "disable-background-apps",
                        Name = "Desactivar Apps en Segundo Plano",
                        Description = "Desactiva la ejecución de aplicaciones UWP de Windows en segundo plano.\n⚠️ Consecuencia: Las aplicaciones de la tienda de Windows no se sincronizarán ni recibirán notificaciones en segundo plano.",
                        Category = "performance",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-diagnostics-services",
                        Name = "Desactivar Servicios de Diagnóstico",
                        Description = "Desactiva servicios de diagnóstico y telemetría avanzada de Windows (DPS, WdiServiceHost, WdiSystemHost, diagsvc).\n⚠️ Consecuencia: Windows no recopilará ni informará problemas complejos de diagnóstico de hardware o software.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-unnecessary-services",
                        Name = "Desactivar Servicios Innecesarios",
                        Description = "Desactiva servicios del sistema redundantes o raramente usados (GraphicsPerfSvc, PcaSvc, Wecsvc).\n⚠️ Consecuencia: Se desactivará el Asistente de Compatibilidad de Programas, por lo que Windows no avisará de programas antiguos con problemas conocidos.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-xbox-services",
                        Name = "Desactivar Servicios de Xbox",
                        Description = "Desactiva los servicios de Xbox Live (Save, NetApi, Gip, AuthManager) para liberar memoria.\n⚠️ Consecuencia: No podrás sincronizar partidas guardadas de Xbox ni conectarte a los servidores de Xbox Live desde el PC.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-bluetooth-services",
                        Name = "Desactivar Servicios de Bluetooth",
                        Description = "Desactiva el servicio de soporte y gateway de Bluetooth (bthserv, BTAGService).\n⚠️ Consecuencia: Los dispositivos Bluetooth (auriculares, mandos, ratones) no funcionarán ni se detectarán en el equipo.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-maps-service",
                        Name = "Desactivar Administrador de Mapas",
                        Description = "Desactiva el servicio MapsBroker encargado de descargar mapas sin conexión.\n⚠️ Consecuencia: No se podrán buscar ni descargar mapas offline de la app nativa de Windows.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-fax-service",
                        Name = "Desactivar Servicio de Fax",
                        Description = "Desactiva el servicio de Fax heredado de Windows.\n⚠️ Consecuencia: No se podrán enviar ni recibir faxes usando el modem del equipo.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-remote-services",
                        Name = "Desactivar Registro Remoto",
                        Description = "Desactiva el servicio RemoteRegistry por seguridad.\n⚠️ Consecuencia: Los administradores de red no podrán modificar el registro de esta máquina de forma remota.",
                        Category = "services",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "optimize-visual-performance",
                        Name = "Optimizar Efectos Visuales",
                        Description = "Ajusta Windows para obtener el mejor rendimiento desactivando animaciones y efectos visuales redundantes.\n⚠️ Consecuencia: Las ventanas se abrirán y cerrarán de forma instantánea sin animaciones de transición, y el Explorador de Windows se sentirá más plano.",
                        Category = "visual",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-notifications",
                        Name = "Desactivar Notificaciones del Sistema",
                        Description = "Desactiva el sistema global de notificaciones y alertas en el Centro de Actividades.\n⚠️ Consecuencia: No verás banners de alertas ni globos de avisos de aplicaciones o de Windows en la barra de tareas.",
                        Category = "visual",
                        Risk = "safe"
                    },
                    new Tweak {
                        Key = "disable-windows-update-auto",
                        Name = "Desactivar Descargas Automáticas de Windows Update",
                        Description = "Configura Windows Update para notificar antes de descargar e instalar actualizaciones.\n⚠️ Consecuencia: Deberás buscar e instalar manualmente las actualizaciones de seguridad en la configuración de Windows Update.",
                        Category = "services",
                        Risk = "moderate"
                    },
                    new Tweak {
                        Key = "disable-power-throttling",
                        Name = "Desactivar CPU Power Throttling",
                        Description = "Desactiva la limitación de potencia por núcleo para tareas de fondo en procesadores modernos.\n⚠️ Consecuencia: Aumentará ligeramente el consumo energético de la CPU en laptops a cambio de mejor respuesta en tareas de fondo.",
                        Category = "performance",
                        Risk = "safe"
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

                        case "disable-background-apps":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-diagnostics-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\diagsvc", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DPS", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\diagnosticshub.standardcollector.service", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WdiServiceHost", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WdiSystemHost", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop DiagTrack");
                            RunCmd("sc", "stop dmwappushservice");
                            RunCmd("sc", "stop diagsvc");
                            RunCmd("sc", "stop DPS");
                            RunCmd("sc", "stop diagnosticshub.standardcollector.service");
                            RunCmd("sc", "stop WdiServiceHost");
                            RunCmd("sc", "stop WdiSystemHost");
                            break;

                        case "disable-unnecessary-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\GraphicsPerfSvc", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Wecsvc", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop GraphicsPerfSvc");
                            RunCmd("sc", "stop PcaSvc");
                            RunCmd("sc", "stop Wecsvc");
                            break;

                        case "disable-xbox-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxGipSvc", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop XblGameSave");
                            RunCmd("sc", "stop XboxNetApiSvc");
                            RunCmd("sc", "stop XboxGipSvc");
                            RunCmd("sc", "stop XblAuthManager");
                            break;

                        case "disable-bluetooth-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\BTAGService", "Start", 4, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\bthserv", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop BTAGService");
                            RunCmd("sc", "stop bthserv");
                            break;

                        case "disable-maps-service":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop MapsBroker");
                            break;

                        case "disable-fax-service":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop Fax");
                            break;

                        case "disable-remote-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RemoteRegistry", "Start", 4, RegistryValueKind.DWord);
                            RunCmd("sc", "stop RemoteRegistry");
                            break;

                        case "optimize-visual-performance":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, RegistryValueKind.DWord);
                            break;

                        case "disable-notifications":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-windows-update-auto":
                            {
                                string auPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
                                Registry.SetValue(auPath, "AUOptions", 2, RegistryValueKind.DWord);
                                Registry.SetValue(auPath, "NoAutoUpdate", 0, RegistryValueKind.DWord);
                            }
                            break;

                        case "disable-power-throttling":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, RegistryValueKind.DWord);
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

                        case "disable-background-apps":
                            try
                            {
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", true))
                                {
                                    k?.SetValue("GlobalUserDisabled", 0, RegistryValueKind.DWord);
                                }
                                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search", true))
                                {
                                    k?.SetValue("BackgroundAppGlobalToggle", 1, RegistryValueKind.DWord);
                                }
                            }
                            catch { }
                            break;

                        case "disable-diagnostics-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack", "Start", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\dmwappushservice", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\diagsvc", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DPS", "Start", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\diagnosticshub.standardcollector.service", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WdiServiceHost", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WdiSystemHost", "Start", 3, RegistryValueKind.DWord);
                            RunCmd("sc", "start DiagTrack");
                            RunCmd("sc", "start DPS");
                            break;

                        case "disable-unnecessary-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\GraphicsPerfSvc", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc", "Start", 2, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Wecsvc", "Start", 3, RegistryValueKind.DWord);
                            RunCmd("sc", "start PcaSvc");
                            break;

                        case "disable-xbox-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxGipSvc", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager", "Start", 3, RegistryValueKind.DWord);
                            break;

                        case "disable-bluetooth-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\BTAGService", "Start", 3, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\bthserv", "Start", 3, RegistryValueKind.DWord);
                            break;

                        case "disable-maps-service":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker", "Start", 2, RegistryValueKind.DWord);
                            break;

                        case "disable-fax-service":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax", "Start", 3, RegistryValueKind.DWord);
                            break;

                        case "disable-remote-services":
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RemoteRegistry", "Start", 3, RegistryValueKind.DWord);
                            break;

                        case "optimize-visual-performance":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0, RegistryValueKind.DWord);
                            break;

                        case "disable-notifications":
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION", 1, RegistryValueKind.DWord);
                            break;

                        case "disable-windows-update-auto":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", true))
                                {
                                    k?.DeleteValue("AUOptions", false);
                                    k?.DeleteValue("NoAutoUpdate", false);
                                }
                            }
                            catch { }
                            break;

                        case "disable-power-throttling":
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", true))
                                {
                                    k?.DeleteValue("PowerThrottlingOff", false);
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
