using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Microsoft.Win32;

namespace cchay_optimicer_cs.Services
{
    public class BloatwareApp
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Packages { get; set; } = new List<string>();
        public bool Installed { get; set; }
    }

    public class BloatwareService
    {
        private static readonly List<BloatwareApp> PredefinedBloatware = new List<BloatwareApp>
        {
            new BloatwareApp {
                Key = "xbox",
                Name = "Xbox App Suite",
                Description = "Xbox App, Gaming Overlay, SpeechToText y servicios relacionados.",
                Packages = new List<string> { "Microsoft.XboxApp", "Microsoft.XboxGamingOverlay", "Microsoft.Xbox.TCUI", "Microsoft.XboxSpeechToTextOverlay", "Microsoft.XboxIdentityProvider" }
            },
            new BloatwareApp {
                Key = "weather",
                Name = "MSN Clima (Weather)",
                Description = "Aplicación del tiempo preinstalada de Bing.",
                Packages = new List<string> { "Microsoft.BingWeather" }
            },
            new BloatwareApp {
                Key = "maps",
                Name = "Mapas de Windows",
                Description = "Servicio y mapas nativos de Windows.",
                Packages = new List<string> { "Microsoft.WindowsMaps" }
            },
            new BloatwareApp {
                Key = "people",
                Name = "Contactos (People)",
                Description = "Aplicación de contactos integrada.",
                Packages = new List<string> { "Microsoft.People" }
            },
            new BloatwareApp {
                Key = "skype",
                Name = "Skype App",
                Description = "Cliente de Skype UWP preinstalado.",
                Packages = new List<string> { "Microsoft.SkypeApp" }
            },
            new BloatwareApp {
                Key = "solitaire",
                Name = "Solitaire Collection",
                Description = "Juegos de cartas con publicidad integrada de Microsoft.",
                Packages = new List<string> { "Microsoft.MicrosoftSolitaireCollection" }
            },
            new BloatwareApp {
                Key = "cortana",
                Name = "Cortana App",
                Description = "Asistente de voz deprecado de Microsoft.",
                Packages = new List<string> { "Microsoft.549981C3F5F10" }
            },
            new BloatwareApp {
                Key = "feedback",
                Name = "Centro de Opiniones",
                Description = "Feedback Hub para reportes y telemetría de experiencia.",
                Packages = new List<string> { "Microsoft.WindowsFeedbackHub" }
            },
            new BloatwareApp {
                Key = "yourphone",
                Name = "Enlace Móvil (Your Phone)",
                Description = "Sincronización con teléfonos Android/iOS.",
                Packages = new List<string> { "Microsoft.YourPhone" }
            },
            new BloatwareApp {
                Key = "mixedreality",
                Name = "Portal de Realidad Mixta",
                Description = "Portal y visor para VR/MR de Windows.",
                Packages = new List<string> { "Microsoft.MixedReality.Portal" }
            },
            new BloatwareApp {
                Key = "3dviewer",
                Name = "Visor 3D",
                Description = "Visualizador de modelos 3D.",
                Packages = new List<string> { "Microsoft.Microsoft3DViewer" }
            },
            new BloatwareApp {
                Key = "bingnews",
                Name = "Noticias de Bing",
                Description = "Noticias MSN en el menú de inicio.",
                Packages = new List<string> { "Microsoft.BingNews" }
            },
            new BloatwareApp {
                Key = "bingsports",
                Name = "Deportes de Bing",
                Description = "MSN Deportes.",
                Packages = new List<string> { "Microsoft.BingSports" }
            }
        };

        private static void RunPowerShell(string script)
        {
            try
            {
                var base64Script = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(15000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to run PowerShell script: " + ex.Message);
            }
        }

        public static Task<List<BloatwareApp>> GetBloatwareListAsync()
        {
            return Task.Run(() =>
            {
                var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var pm = new PackageManager();
                    // Get packages for current user (extremely fast)
                    var packages = pm.FindPackages();
                    foreach (var p in packages)
                    {
                        try
                        {
                            installedNames.Add(p.Id.Name);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to get installed packages: " + ex.Message);
                }

                var list = new List<BloatwareApp>();
                foreach (var def in PredefinedBloatware)
                {
                    bool isInstalled = def.Packages.Any(pkg => installedNames.Contains(pkg));
                    list.Add(new BloatwareApp
                    {
                        Key = def.Key,
                        Name = def.Name,
                        Description = def.Description,
                        Packages = def.Packages,
                        Installed = isInstalled
                    });
                }
                return list;
            });
        }

        public static Task<bool> UninstallAppAsync(string key)
        {
            return Task.Run(() =>
            {
                var app = PredefinedBloatware.FirstOrDefault(d => d.Key == key);
                if (app == null) return false;

                try
                {
                    // Run deep uninstallation for each package in definition
                    foreach (var pkg in app.Packages)
                    {
                        string script = $@"
                            Get-AppxPackage -Name '{pkg}' -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
                            Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ $_.PackageName -like '*{pkg}*' }} | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
                        ";
                        RunPowerShell(script);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> DisableOneDriveAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        Stop-Process -Name 'OneDrive' -Force -ErrorAction SilentlyContinue
                        Start-Sleep -Seconds 1

                        $sysWowPath = ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe""
                        $sys32Path = ""$env:SystemRoot\System32\OneDriveSetup.exe""
                        
                        if (Test-Path $sysWowPath) {
                          Start-Process $sysWowPath -ArgumentList '/uninstall' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                        } elseif (Test-Path $sys32Path) {
                          Start-Process $sys32Path -ArgumentList '/uninstall' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                        }

                        $registryPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\OneDrive'
                        if (!(Test-Path $registryPath)) {
                          New-Item -Path $registryPath -Force | Out-Null
                        }
                        Set-ItemProperty -Path $registryPath -Name 'DisableFileSyncNGSC' -Value 1 -Force -ErrorAction SilentlyContinue
                    ";
                    RunPowerShell(script);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> DisableWidgetsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        Get-AppxPackage -Name 'MicrosoftWindows.Client.WebExperience' -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
                        Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ $_.PackageName -like '*MicrosoftWindows.Client.WebExperience*' }} | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue
                    ";
                    RunPowerShell(script);
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
