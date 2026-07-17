using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class OptimizationProfile
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "#40C057";
        public HashSet<string> ServicesToDisable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class WindowsServicesService
    {
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
                process?.WaitForExit(5000);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        //  OPTIMIZATION PROFILES
        // ═══════════════════════════════════════════════════════════════

        // Base: Telemetry / Diagnostic / clearly unnecessary
        private static readonly HashSet<string> RecommendedServices = new(StringComparer.OrdinalIgnoreCase)
        {
            "diagtrack",                    // Telemetría de Windows (Connected User Experiences)
            "dmwappushservice",             // WAP Push Message Routing (telemetría)
            "diagsvc",                      // Diagnostic Execution Service
            "diagnosticshub.standardcollector.service", // Diagnostic Hub
            "wdiservicehost",               // Diagnostic Service Host
            "wdisystemhost",                // Diagnostic System Host
            "dps",                          // Diagnostic Policy Service
            "pcasvc",                       // Program Compatibility Assistant
            "wecsvc",                       // Windows Event Collector
            "fax",                          // Servicio de Fax
            "remoteregistry",               // Registro Remoto (riesgo de seguridad)
            "retaildemo",                   // Retail Demo Service
            "wisvc",                        // Windows Insider Service
            "mapsbrokersvc",                // Downloaded Maps Manager (mapas sin usar)
        };

        // Gaming: Everything in Recommended + services that waste CPU/RAM for gamers
        private static readonly HashSet<string> GamingServices = new(StringComparer.OrdinalIgnoreCase)
        {
            // Xbox ecosystem (innecesario si no usas Xbox)
            "xblgamesave",                  // Xbox Live Game Save
            "xboxnetapisvc",                // Xbox Live Networking Service
            "xboxgipsvc",                   // Xbox Accessory Management
            "xblauthmanager",               // Xbox Live Auth Manager
            "gameinputsvc",                 // GameInput Service
            // Print (gamers rarely print mid-game)
            "spooler",                      // Print Spooler
            // Bluetooth (si usas cable, no necesitas)
            "btagservice",                  // Bluetooth Audio Gateway
            "bthserv",                      // Bluetooth Support Service
            // Search indexing (devora disco I/O)
            "wsearch",                      // Windows Search (indexación constante)
            // Graphics telemetry
            "graphicsperfsvc",              // Graphics performance monitor (telemetría GPU)
            // SysMain / Superfetch (interfiere con juegos)
            "sysmain",                      // SysMain/Superfetch (pre-carga de apps)
            // Windows Error Reporting
            "wersvc",                       // Windows Error Reporting
            // Tablet/Touch (si usas desktop)
            "tabletinputservice",           // Touch Keyboard and Handwriting
            // OneSyncSvc
            "onesyncsvc",                   // Sync Host (sincronización innecesaria)
        };

        // Maximum: Everything in Gaming + aggressively strip background services
        private static readonly HashSet<string> MaximumServices = new(StringComparer.OrdinalIgnoreCase)
        {
            // Windows Update delivery optimization (P2P)
            "dosvc",                        // Delivery Optimization (P2P de updates)
            // Connected Devices
            "cdpsvc",                       // Connected Devices Platform Service
            "cdpusersvc",                   // Connected Devices Platform User Service
            // Biometric
            "wbiosrvc",                     // Windows Biometric Service
            // Smart Card
            "scardsvr",                     // Smart Card
            "scpolicysvc",                  // Smart Card Removal Policy
            // Phone integration
            "phoneservice",                 // Phone Service
            // Remote Desktop (si no lo usas)
            "termservice",                  // Remote Desktop Services
            "sessionenv",                   // Remote Desktop Configuration
            "umrdpservice",                 // Remote Desktop Services UserMode Port Redirector
            // Geolocation
            "lfsvc",                        // Geolocation Service
            // Wallet
            "walletservice",                // WalletService
            // Offline Files
            "cscservice",                   // Offline Files
            // Windows Media Player sharing
            "wmpnetworksvc",                // Windows Media Player Network Sharing
            // Peer networking
            "pnrpautoregistrationservice",  // Peer Name Resolution (PNRP)
            "p2psvc",                       // Peer Networking Grouping
            "p2pimsvc",                     // Peer Networking Identity Manager
            // AllJoyn Router
            "ajrouter",                     // AllJoyn Router Service
            // Parental Controls
            "wcncsvc",                      // Windows Connect Now
            // Secondary Logon
            "seclogon",                     // Secondary Logon
            // SSDP Discovery (UPnP)
            "ssdpsrv",                      // SSDP Discovery
            "upnphost",                     // UPnP Device Host
        };

        // Services that should NEVER be disabled regardless of profile
        private static readonly HashSet<string> CriticalServices = new(StringComparer.OrdinalIgnoreCase)
        {
            "rpcss", "dcomlaunch", "eventlog", "keyiso", "samss",
            "winmgmt", "plugplay", "brokerinfrastructure", "systemeventsbroker", "staterepository",
            "coremessagingregistrar", "cryptsvc", "gpsvc", "profsvc", "lsass", "comsvcs",
            "ntds", "kdc", "netlogon", "wuauserv", "mpssvc", "windefend", "securityhealthservice",
            "dhcp", "dnscache", "nsi", "bits", "schedule", "sppsvc", "themes", "appinfo",
            "lsm", "power", "sens", "usermanager", "coreui", "tiledatamodelsvc",
            "audiosrv", "audioendpointbuilder", "lanmanserver", "lanmanworkstation",
            "netprofm", "nlasvc", "wcmsvc", "wlansvc", "dot3svc", "shellhwdetection",
            "fontsvc", "statesvc", "dispbrokerdesktopsvc", "displayenhancementservice"
        };

        public static List<OptimizationProfile> GetProfiles()
        {
            return new List<OptimizationProfile>
            {
                new OptimizationProfile
                {
                    Key = "recommended",
                    Name = "🛡️ Recomendado",
                    Description = "Seguro para todos. Desactiva telemetría, diagnósticos y servicios obsoletos sin afectar funcionalidad.",
                    Icon = "ShieldCheckmark24",
                    BadgeColor = "#40C057",
                    ServicesToDisable = RecommendedServices
                },
                new OptimizationProfile
                {
                    Key = "gaming",
                    Name = "🎮 Gaming",
                    Description = "Optimizado para juegos. Desactiva Xbox, Bluetooth, búsqueda indexada, Superfetch y todo lo que robe FPS.",
                    Icon = "Games24",
                    BadgeColor = "#7C3AED",
                    ServicesToDisable = new HashSet<string>(
                        RecommendedServices.Concat(GamingServices),
                        StringComparer.OrdinalIgnoreCase)
                },
                new OptimizationProfile
                {
                    Key = "maximum",
                    Name = "⚡ Máximo Rendimiento",
                    Description = "Agresivo. Deshabilita todo lo no esencial: P2P, geolocalización, UPnP, biometría, escritorio remoto y más.",
                    Icon = "Flash24",
                    BadgeColor = "#FA5252",
                    ServicesToDisable = new HashSet<string>(
                        RecommendedServices.Concat(GamingServices).Concat(MaximumServices),
                        StringComparer.OrdinalIgnoreCase)
                }
            };
        }

        /// <summary>
        /// Returns the set of service names that exist on this machine and match the profile.
        /// </summary>
        public static async Task<List<string>> GetServicesForProfileAsync(string profileKey, List<WindowsServiceInfo> currentServices)
        {
            var profiles = GetProfiles();
            var profile = profiles.FirstOrDefault(p => p.Key == profileKey);
            if (profile == null) return new List<string>();

            // Only return services that:
            //  1. Exist on this machine
            //  2. Are in the profile's disable list
            //  3. Are NOT critical
            //  4. Are NOT already disabled
            return await Task.Run(() =>
            {
                return currentServices
                    .Where(s => profile.ServicesToDisable.Contains(s.Name)
                             && !CriticalServices.Contains(s.Name)
                             && s.StartupType != "Deshabilitado")
                    .Select(s => s.Name)
                    .ToList();
            });
        }

        /// <summary>
        /// Applies a profile: disables and stops all matching services.
        /// Returns the count of services changed.
        /// </summary>
        public static async Task<int> ApplyProfileAsync(string profileKey, List<WindowsServiceInfo> currentServices, Action<string, int, int>? progressCallback = null)
        {
            var toDisable = await GetServicesForProfileAsync(profileKey, currentServices);
            int total = toDisable.Count;
            int done = 0;

            foreach (var serviceName in toDisable)
            {
                done++;
                progressCallback?.Invoke(serviceName, done, total);

                await ChangeServiceStartupTypeAsync(serviceName, "Deshabilitado");
                await ChangeServiceStatusAsync(serviceName, "stop");
            }

            return done;
        }

        private static string GetServiceRiskLevel(string serviceName)
        {
            if (CriticalServices.Contains(serviceName))
                return "critical"; // Intocable / Crítico

            // Bloatware/Innecesario services (union of all profiles)
            var allOptimizable = new HashSet<string>(
                RecommendedServices.Concat(GamingServices).Concat(MaximumServices),
                StringComparer.OrdinalIgnoreCase);

            if (allOptimizable.Contains(serviceName))
                return "bloat"; // Recomendado deshabilitar

            return "normal";
        }

        public static Task<List<WindowsServiceInfo>> GetServicesAsync()
        {
            return Task.Run(() =>
            {
                var list = new List<WindowsServiceInfo>();
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, Description FROM Win32_Service"))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                string name = obj["Name"]?.ToString() ?? "";
                                string displayName = obj["DisplayName"]?.ToString() ?? "";
                                string state = obj["State"]?.ToString() ?? ""; // e.g. "Running", "Stopped"
                                string startMode = obj["StartMode"]?.ToString() ?? ""; // e.g. "Auto", "Manual", "Disabled"
                                string desc = obj["Description"]?.ToString() ?? "";

                                string startupType = startMode switch
                                {
                                    "Auto" => "Automático",
                                    "Manual" => "Manual",
                                    "Disabled" => "Deshabilitado",
                                    _ => startMode
                                };

                                string status = state switch
                                {
                                    "Running" => "En ejecución",
                                    "Stopped" => "Detenido",
                                    _ => state
                                };

                                string riskLevel = GetServiceRiskLevel(name);

                                list.Add(new WindowsServiceInfo
                                {
                                    Name = name,
                                    DisplayName = displayName,
                                    Status = status,
                                    StartupType = startupType,
                                    Description = desc,
                                    RiskLevel = riskLevel
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error getting services: " + ex.Message);
                }
                return list;
            });
        }

        public static Task<bool> ChangeServiceStatusAsync(string name, string action)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (action.Equals("start", StringComparison.OrdinalIgnoreCase))
                    {
                        RunCmd("sc.exe", $"start {name}");
                    }
                    else if (action.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        RunCmd("sc.exe", $"stop {name}");
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> ChangeServiceStartupTypeAsync(string name, string type)
        {
            return Task.Run(() =>
            {
                try
                {
                    string startArg = type.ToLower() switch
                    {
                        "automático" => "auto",
                        "manual" => "demand",
                        "deshabilitado" => "disabled",
                        _ => "demand"
                    };

                    RunCmd("sc.exe", $"config {name} start= {startArg}");
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}

