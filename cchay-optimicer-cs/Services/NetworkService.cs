using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class NetworkService
    {
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CchayOptimicer",
            "networkState.json"
        );

        private static readonly List<DnsProvider> DefaultProviders = new List<DnsProvider>
        {
            new DnsProvider { Key = "cloudflare", Name = "Cloudflare", Description = "El DNS más rápido y privado, excelente para juegos y navegación general.", Primary = "1.1.1.1", Secondary = "1.0.0.1" },
            new DnsProvider { Key = "google", Name = "Google Public DNS", Description = "Gran velocidad y alta fiabilidad en todo el mundo.", Primary = "8.8.8.8", Secondary = "8.8.4.4" },
            new DnsProvider { Key = "opendns", Name = "OpenDNS", Description = "Protección web personalizable y filtrado parental.", Primary = "208.67.222.222", Secondary = "208.67.220.220" },
            new DnsProvider { Key = "adguard", Name = "AdGuard DNS", Description = "Bloquea anuncios, rastreadores y phishing a nivel DNS.", Primary = "94.140.14.14", Secondary = "94.140.15.15" },
            new DnsProvider { Key = "quad9", Name = "Quad9", Description = "Seguridad robusta bloqueando dominios maliciosos conocidos.", Primary = "9.9.9.9", Secondary = "149.112.112.112" }
        };

        public class NetworkState
        {
            public bool TcpTweaksEnabled { get; set; }
        }

        public static NetworkState LoadNetworkState()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    return JsonSerializer.Deserialize<NetworkState>(json) ?? new NetworkState();
                }
            }
            catch { /* ignore */ }
            return new NetworkState();
        }

        public static void SaveNetworkState(NetworkState state)
        {
            try
            {
                string dir = Path.GetDirectoryName(StateFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save network state: {ex.Message}");
            }
        }

        private static bool RunPowerShell(string script)
        {
            try
            {
                var base64Script = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                if (!proc.WaitForExit(15000))
                {
                    proc.Kill(true);
                    return false;
                }
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to run PowerShell script: " + ex.Message);
                return false;
            }
        }

        public static List<DnsProvider> GetDnsProviders()
        {
            return DefaultProviders.Select(p => new DnsProvider
            {
                Key = p.Key,
                Name = p.Name,
                Description = p.Description,
                Primary = p.Primary,
                Secondary = p.Secondary,
                Ping = null
            }).ToList();
        }

        public static async Task<int> PingIpAsync(string ip)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        return (int)reply.RoundtripTime;
                    }
                }
            }
            catch { /* ignore */ }
            return 999;
        }

        public static async Task<List<DnsProvider>> TestAllPingsAsync()
        {
            var providers = GetDnsProviders();
            var tasks = providers.Select(async provider =>
            {
                var t1 = PingIpAsync(provider.Primary);
                var t2 = PingIpAsync(provider.Secondary);
                var pings = await Task.WhenAll(t1, t2);
                provider.Ping = pings.Min();
            });
            await Task.WhenAll(tasks);
            return providers.OrderBy(p => p.Ping ?? 999).ToList();
        }

        public static Task<bool> SetDnsAsync(string[] servers)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (servers is null || servers.Length is < 1 or > 2 || servers.Any(s => !IPAddress.TryParse(s, out _)))
                        return false;

                    string serverStr = string.Join(",", servers.Select(s => $"'{s}'"));
                    string script = $@"
                        $ErrorActionPreference = 'Stop'
                        $adapters = Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }}
                        if (-not $adapters) {{ exit 2 }}
                        foreach ($a in $adapters) {{
                            Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ({serverStr}) -ErrorAction Stop
                        }}
                    ";
                    return RunPowerShell(script);
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> ResetDnsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        $ErrorActionPreference = 'Stop'
                        $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
                        if (-not $adapters) { exit 2 }
                        foreach ($a in $adapters) {
                            Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses -ErrorAction Stop
                        }
                    ";
                    return RunPowerShell(script);
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> FlushDnsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    return RunPowerShell("$ErrorActionPreference = 'Stop'; Clear-DnsClientCache -ErrorAction Stop");
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> ApplyTcpTweaksAsync(bool enabled)
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = enabled ? @"
                        netsh int tcp set global autotuninglevel=normal; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global rss=enabled; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global ecncapability=enabled; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global timestamps=disabled; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                    " : @"
                        netsh int tcp set global autotuninglevel=normal; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global rss=default; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global ecncapability=default; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                        netsh int tcp set global timestamps=default; if ($LASTEXITCODE) { exit $LASTEXITCODE }
                    ";
                    if (!RunPowerShell(script)) return false;
                    SaveNetworkState(new NetworkState { TcpTweaksEnabled = enabled });
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> ApplyQosPoliciesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        New-NetQosPolicy -Name 'CchayGamingQoS' -AppPathNameMatchCondition '*cs2.exe*' -ThrottleRateActionBitsPerSecond 0 -DSCPAction 46 -ErrorAction SilentlyContinue 2>$null
                        New-NetQosPolicy -Name 'CchayDiscordQoS' -AppPathNameMatchCondition '*Discord.exe*' -ThrottleRateActionBitsPerSecond 0 -DSCPAction 46 -ErrorAction SilentlyContinue 2>$null
                    ";
                    return RunPowerShell(script);
                }
                catch
                {
                    return false;
                }
            });
        }

        public static Task<bool> RemoveQosPoliciesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        Remove-NetQosPolicy -Name 'CchayGamingQoS' -Confirm:$false -ErrorAction SilentlyContinue 2>$null
                        Remove-NetQosPolicy -Name 'CchayDiscordQoS' -Confirm:$false -ErrorAction SilentlyContinue 2>$null
                    ";
                    return RunPowerShell(script);
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
