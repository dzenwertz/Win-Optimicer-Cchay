using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace cchay_optimicer_cs.Services
{
    public class TelemetryStatus
    {
        public bool HostsBlocked { get; set; }
        public bool FirewallRulesApplied { get; set; }
        public bool ServicesDisabled { get; set; }
        public int BlockedDomainsCount { get; set; }
    }

    public class TelemetryService
    {
        private static readonly string HostsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"drivers\etc\hosts"
        );

        private const string SectionHeader = "# === CCHAY OPTIMICER TELEMETRY BLOCK === #";
        private const string SectionFooter = "# === END CCHAY OPTIMICER TELEMETRY BLOCK === #";

        private static readonly string[] TelemetryDomains = new string[]
        {
            "v10.events.data.microsoft.com", "v20.events.data.microsoft.com", "watson.telemetry.microsoft.com",
            "telemetry.microsoft.com", "diagnostics.support.microsoft.com", "feedback.windows.com",
            "telecommand.telemetry.microsoft.com", "telecommand.telemetry.microsoft.com.nsatc.net",
            "oca.telemetry.microsoft.com", "sqm.telemetry.microsoft.com", "statsfe1.ws.microsoft.com",
            "statsfe2.ws.microsoft.com", "survey.watson.microsoft.com", "view.atdmt.com",
            "compatex.telemetry.microsoft.com", "data.microsoft.com", "a-0001.a-msedge.net",
            "choice.microsoft.com", "choice.microsoft.com.nsatc.net", "df.telemetry.microsoft.com",
            "reports.wes.df.telemetry.microsoft.com", "services.wes.df.telemetry.microsoft.com",
            "sqm.df.telemetry.microsoft.com", "telemetry.urs.microsoft.com", "telemetry.appex.bing.net",
            "settings-win.data.microsoft.com", "v10.vortex-win.data.microsoft.com", "v20.vortex-win.data.microsoft.com",
            "telemetry.nvidia.com", "gfe.nvidia.com", "events.gfe.nvidia.com",
            "cc-api-data.adobe.io"
        };

        public static Task<TelemetryStatus> GetStatusAsync()
        {
            return Task.Run(() =>
            {
                var status = new TelemetryStatus();
                try
                {
                    if (File.Exists(HostsFilePath))
                    {
                        string content = File.ReadAllText(HostsFilePath);
                        status.HostsBlocked = content.Contains(SectionHeader);
                    }

                    status.BlockedDomainsCount = TelemetryDomains.Length;

                    // Check services status
                    try
                    {
                        string out1 = RunCmdWithOutput("sc", "query DiagTrack");
                        string out2 = RunCmdWithOutput("sc", "query dmwappushservice");
                        status.ServicesDisabled = out1.Contains("STOPPED") && out2.Contains("STOPPED");
                    }
                    catch
                    {
                        status.ServicesDisabled = false;
                    }

                    status.FirewallRulesApplied = CheckFirewallRuleExists();
                }
                catch { }

                return status;
            });
        }

        public static Task EnableFullShieldAsync()
        {
            return Task.Run(async () =>
            {
                await ApplyHostsBlockAsync();
                await ApplyFirewallRulesAsync();
                await DisableTelemetryServicesAsync();
            });
        }

        public static Task DisableFullShieldAsync()
        {
            return Task.Run(async () =>
            {
                await RemoveHostsBlockAsync();
                await RemoveFirewallRulesAsync();
                await EnableTelemetryServicesAsync();
            });
        }

        private static Task ApplyHostsBlockAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(HostsFilePath)) return;

                    string content = File.ReadAllText(HostsFilePath);
                    if (content.Contains(SectionHeader)) return; // Already applied

                    var sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine(SectionHeader);
                    foreach (var domain in TelemetryDomains)
                    {
                        sb.AppendLine($"0.0.0.0 {domain}");
                        sb.AppendLine($"0.0.0.0 www.{domain}");
                    }
                    sb.AppendLine(SectionFooter);

                    File.AppendAllText(HostsFilePath, sb.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TelemetryService] Error applying hosts block: {ex.Message}");
                }
            });
        }

        private static Task RemoveHostsBlockAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(HostsFilePath)) return;

                    string[] lines = File.ReadAllLines(HostsFilePath);
                    var cleanLines = new List<string>();
                    bool insideSection = false;

                    foreach (var line in lines)
                    {
                        if (line.Contains(SectionHeader))
                        {
                            insideSection = true;
                            continue;
                        }
                        if (line.Contains(SectionFooter))
                        {
                            insideSection = false;
                            continue;
                        }
                        if (!insideSection)
                        {
                            cleanLines.Add(line);
                        }
                    }

                    File.WriteAllLines(HostsFilePath, cleanLines);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TelemetryService] Error removing hosts block: {ex.Message}");
                }
            });
        }

        private static Task ApplyFirewallRulesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    RunCmd("netsh", "advfirewall firewall delete rule name=\"CchayTelemetryBlock\"");
                    string ruleName = "RethTelemetryBlock";
                    RunCmd("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\"");
                    string remoteIps = "184.86.0.0/16,191.232.0.0/13,13.107.4.0/22";
                    RunCmd("netsh", $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block remoteip={remoteIps}");
                }
                catch { }
            });
        }

        private static Task RemoveFirewallRulesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    RunCmd("netsh", "advfirewall firewall delete rule name=\"CchayTelemetryBlock\"");
                    RunCmd("netsh", "advfirewall firewall delete rule name=\"RethTelemetryBlock\"");
                }
                catch { }
            });
        }

        private static bool CheckFirewallRuleExists()
        {
            try
            {
                string output = RunCmdWithOutput("netsh", "advfirewall firewall show rule name=\"RethTelemetryBlock\"");
                if (output.Contains("RethTelemetryBlock")) return true;
                string legacy = RunCmdWithOutput("netsh", "advfirewall firewall show rule name=\"CchayTelemetryBlock\"");
                return legacy.Contains("CchayTelemetryBlock");
            }
            catch
            {
                return false;
            }
        }

        private static Task DisableTelemetryServicesAsync()
        {
            return Task.Run(() =>
            {
                string[] services = { "DiagTrack", "dmwappushservice" };
                foreach (var svc in services)
                {
                    try
                    {
                        RunCmd("sc", $"config \"{svc}\" start= disabled");
                        RunCmd("sc", $"stop \"{svc}\"");
                    }
                    catch { }
                }

                // Registry tweaks for telemetry
                try
                {
                    string regPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
                    Registry.SetValue(regPath, "AllowTelemetry", 0, RegistryValueKind.DWord);
                }
                catch { }
            });
        }

        private static Task EnableTelemetryServicesAsync()
        {
            return Task.Run(() =>
            {
                string[] services = { "DiagTrack", "dmwappushservice" };
                foreach (var svc in services)
                {
                    try
                    {
                        RunCmd("sc", $"config \"{svc}\" start= auto");
                        RunCmd("sc", $"start \"{svc}\"");
                    }
                    catch { }
                }

                try
                {
                    string regPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
                    Registry.SetValue(regPath, "AllowTelemetry", 1, RegistryValueKind.DWord);
                }
                catch { }
            });
        }

        private static void RunCmd(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }

        private static string RunCmdWithOutput(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "";
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return output;
        }
    }
}
