using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using cchay_optimicer_cs.Services;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs_tests
{
    public class ServiceTests
    {
        [Fact]
        public void TestRamService()
        {
            var mem = RamService.GetMemoryUsage();
            Assert.NotEqual(0UL, mem.TotalBytes);
            Assert.True(mem.FreeBytes <= mem.TotalBytes);
            Assert.InRange(mem.PercentUsed, 0, 100);
        }

        [Fact]
        public async Task TestSystemServiceInfo()
        {
            var info = await SystemService.GetSystemInfoAsync();
            Assert.False(string.IsNullOrEmpty(info.CpuModel));
            Assert.True(info.CpuCores > 0);
            Assert.True(info.CpuThreads > 0);
            Assert.False(string.IsNullOrEmpty(info.GpuModel));
            Assert.False(string.IsNullOrEmpty(info.OsName));
            Assert.True(info.DiskTotal > 0);
            Assert.InRange(info.HealthScore, 0, 100);

            if (info.CpuTemperature != "N/A" && info.CpuTemperature != "N/A / Bloqueado")
            {
                Assert.EndsWith(" °C", info.CpuTemperature);
            }
        }

        [Fact]
        public async Task TestTweakService()
        {
            var tweaks = await TweakService.GetTweaksAsync();
            Assert.NotNull(tweaks);
            Assert.NotEmpty(tweaks);

            var disableBackground = tweaks.FirstOrDefault(t => t.Key == "disable-background-apps");
            Assert.NotNull(disableBackground);

            var powerThrottling = tweaks.FirstOrDefault(t => t.Key == "disable-power-throttling");
            Assert.NotNull(powerThrottling);

            foreach (var t in tweaks)
            {
                Assert.False(string.IsNullOrEmpty(t.Key));
                Assert.False(string.IsNullOrEmpty(t.Name));
                Assert.False(string.IsNullOrEmpty(t.Description));
                Assert.False(string.IsNullOrEmpty(t.Category));
            }
        }

        [Fact]
        public async Task TestWindowsServices()
        {
            var services = await WindowsServicesService.GetServicesAsync();
            Assert.NotNull(services);
            Assert.NotEmpty(services);

            var rpcss = services.FirstOrDefault(s => s.Name.Equals("rpcss", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rpcss);
            Assert.Equal("critical", rpcss.RiskLevel);

            var diagtrack = services.FirstOrDefault(s => s.Name.Equals("diagtrack", StringComparison.OrdinalIgnoreCase));
            if (diagtrack != null)
            {
                Assert.Equal("bloat", diagtrack.RiskLevel);
            }
        }

        [Fact]
        public async Task TestDeviceCleanup()
        {
            var devices = await DeviceCleanupService.GetDisconnectedDevicesAsync();
            Assert.NotNull(devices);

            foreach (var dev in devices)
            {
                Assert.False(string.IsNullOrEmpty(dev.InstanceId));
            }
        }

        [Fact]
        public void TestProfileService()
        {
            var gaming = ProfileService.GetGamingPreset();
            Assert.Equal("Perfil Gamer Máximo Rendimiento", gaming.ProfileName);
            Assert.True(gaming.TweakStates.ContainsKey("ultimate-performance") && gaming.TweakStates["ultimate-performance"]);

            var balanced = ProfileService.GetBalancedPreset();
            Assert.Equal("Perfil Equilibrado / Diario", balanced.ProfileName);

            var dev = ProfileService.GetDeveloperPreset();
            Assert.Contains("Desarrollador", dev.ProfileName);
            Assert.True(dev.TweakStates.ContainsKey("disable-superfetch") && dev.TweakStates["disable-superfetch"]);
        }

        [Fact]
        public async Task TestDiskAnalyzerService()
        {
            string tempDir = Path.GetTempPath();
            var items = await DiskAnalyzerService.ScanDirectoryAsync(tempDir);
            Assert.NotNull(items);
        }

        [Fact]
        public async Task TestBenchmarkService()
        {
            var res = await BenchmarkService.RunBenchmarkAsync();
            Assert.NotNull(res);
            Assert.InRange(res.TotalScore, 50, 1000);
            Assert.False(string.IsNullOrEmpty(res.Grade));
        }

        [Fact]
        public async Task TestAntiStutterService()
        {
            SettingsService.Settings.AntiStutterEnabled = true;
            await AntiStutterService.CheckAndPurgeIfNeededAsync();
            AntiStutterService.IsCooldownActive();
        }

        [Fact]
        public async Task TestTelemetryService()
        {
            var status = await TelemetryService.GetStatusAsync();
            Assert.NotNull(status);
            Assert.True(status.BlockedDomainsCount > 0);
        }

        [Fact]
        public async Task TestUninstallerService()
        {
            var apps = await UninstallerService.GetInstalledProgramsAsync();
            Assert.NotNull(apps);
        }

        [Fact]
        public async Task TestNetworkService()
        {
            var providers = NetworkService.GetDnsProviders();
            Assert.NotNull(providers);
            Assert.NotEmpty(providers);

            var state = NetworkService.LoadNetworkState();
            Assert.NotNull(state);

            bool result = await NetworkService.SetDnsAsync(new[] { "not-an-ip" });
            Assert.False(result);
        }

        [Fact]
        public void TestAllXamlSymbolIcons()
        {
            string xamlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "cchay-optimicer-cs");
            if (!Directory.Exists(xamlDir)) return;

            var files = Directory.GetFiles(xamlDir, "*.xaml", SearchOption.AllDirectories);
            var regex = new Regex(@"(?:Symbol|Icon)=(?:""|)?([a-zA-Z0-9_]+)", RegexOptions.Compiled);

            var invalidSymbols = new List<string>();
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                var matches = regex.Matches(text);
                foreach (Match match in matches)
                {
                    string symName = match.Groups[1].Value;
                    try
                    {
                        Enum.Parse(typeof(Wpf.Ui.Controls.SymbolRegular), symName);
                    }
                    catch
                    {
                        invalidSymbols.Add($"{Path.GetFileName(file)}: {symName}");
                    }
                }
            }

            Assert.Empty(invalidSymbols);
        }
    }
}
