using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                    string serverStr = string.Join(",", servers.Select(s => $"'{s}'"));
                    string script = $@"
                        $adapters = Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }}
                        if ($adapters) {{
                            foreach ($a in $adapters) {{
                                Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ServerAddresses ({serverStr}) -ErrorAction SilentlyContinue
                            }}
                        }}
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

        public static Task<bool> ResetDnsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = @"
                        $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
                        if ($adapters) {
                            foreach ($a in $adapters) {
                                Set-DnsClientServerAddress -InterfaceIndex $a.InterfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue
                            }
                        }
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

        public static Task<bool> FlushDnsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string script = "Clear-DnsClientCache";
                    RunPowerShell(script);
                    return true;
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
                        netsh int tcp set global autotuninglevel=normal 2>$null
                        netsh int tcp set global rss=enabled 2>$null
                        netsh int tcp set global chimney=enabled 2>$null
                        netsh int tcp set global dca=enabled 2>$null
                        netsh int tcp set global netdma=enabled 2>$null
                        netsh int tcp set global ecncapability=enabled 2>$null
                        netsh int tcp set global timestamps=disabled 2>$null
                    " : @"
                        netsh int tcp set global autotuninglevel=normal 2>$null
                        netsh int tcp set global rss=default 2>$null
                        netsh int tcp set global chimney=default 2>$null
                        netsh int tcp set global dca=default 2>$null
                        netsh int tcp set global netdma=default 2>$null
                        netsh int tcp set global ecncapability=default 2>$null
                        netsh int tcp set global timestamps=default 2>$null
                    ";
                    RunPowerShell(script);
                    SaveNetworkState(new NetworkState { TcpTweaksEnabled = enabled });
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
