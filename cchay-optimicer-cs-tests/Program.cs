using System;
using System.Linq;
using System.Threading.Tasks;
using cchay_optimicer_cs.Services;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs_tests
{
    class Program
    {
        static int _passed = 0;
        static int _failed = 0;

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("        CCHAY OPTIMICER - AUTOMATED TEST SUITE    ");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            // Run tests
            await AssertTest("RAM Service Memory Usage Detection", TestRamService);
            await AssertTest("System Info Diagnostics (CPU, Temp, SMART)", TestSystemServiceInfo);
            await AssertTest("Registry Tweaks Integrity & Structure", TestTweakService);
            await AssertTest("Windows Services Enumeration & Critical Tagging", TestWindowsServices);
            await AssertTest("Disconnected Devices (Ghost Drivers) Scan", TestDeviceCleanup);
            await AssertTest("Optimization Profile Presets & Import/Export", TestProfileService);
            await AssertTest("Disk Analyzer Directory Size Scanner", TestDiskAnalyzerService);
            await AssertTest("Benchmark Engine (Disk, RAM & CPU Speed Test)", TestBenchmarkService);
            await AssertTest("Anti-Stuttering Engine Proactive Memory Purge", TestAntiStutterService);
            await AssertTest("Telemetry Shield & Domain Blocking Status", TestTelemetryService);
            await AssertTest("Uninstaller Registry Application Enumeration", TestUninstallerService);
            await AssertTest("Network Pro & QoS Policy Tuning Engine", TestNetworkService);
            await AssertTest("XAML Symbol Icons Integrity & Validity", TestAllXamlSymbolIcons);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.Write("TEST RESULTS: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{_passed} PASSED");
            Console.ResetColor();
            Console.Write(" / ");
            Console.ForegroundColor = _failed > 0 ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine($"{_failed} FAILED");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.ResetColor();

            Environment.Exit(_failed > 0 ? 1 : 0);
        }

        static async Task AssertTest(string testName, Func<Task> testFunc)
        {
            Console.Write($"Running: {testName,-50} ... ");
            try
            {
                await testFunc();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASSED]");
                Console.ResetColor();
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAILED]");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"   Error: {ex.Message}");
                Console.ResetColor();
                _failed++;
            }
        }

        static Task TestRamService()
        {
            var mem = RamService.GetMemoryUsage();
            if (mem.TotalBytes == 0) throw new Exception("Total RAM bytes reported as 0");
            if (mem.FreeBytes > mem.TotalBytes) throw new Exception("Free RAM bytes greater than Total RAM");
            if (mem.PercentUsed < 0 || mem.PercentUsed > 100) throw new Exception($"Invalid RAM percentage: {mem.PercentUsed}%");
            return Task.CompletedTask;
        }

        static async Task TestSystemServiceInfo()
        {
            var info = await SystemService.GetSystemInfoAsync();
            if (string.IsNullOrEmpty(info.CpuModel)) throw new Exception("CPU Model string is null or empty");
            if (info.CpuCores <= 0) throw new Exception($"Invalid CPU Core count: {info.CpuCores}");
            if (info.CpuThreads <= 0) throw new Exception($"Invalid CPU Thread count: {info.CpuThreads}");
            if (string.IsNullOrEmpty(info.GpuModel)) throw new Exception("GPU Model is null or empty");
            if (string.IsNullOrEmpty(info.OsName)) throw new Exception("OS name is null or empty");
            if (info.DiskTotal <= 0) throw new Exception("Disk total bytes is 0 or negative");
            if (info.HealthScore < 0 || info.HealthScore > 100) throw new Exception($"Health score out of range: {info.HealthScore}");
            
            // Check formatted temperature format
            if (info.CpuTemperature != "N/A" && info.CpuTemperature != "N/A / Bloqueado")
            {
                if (!info.CpuTemperature.EndsWith(" °C")) throw new Exception($"Invalid temperature format: {info.CpuTemperature}");
            }
        }

        static async Task TestTweakService()
        {
            var tweaks = await TweakService.GetTweaksAsync();
            if (tweaks == null || tweaks.Count == 0) throw new Exception("No tweaks returned from TweakService");
            
            // Confirm the new tweaks exist
            var disableBackground = tweaks.FirstOrDefault(t => t.Key == "disable-background-apps");
            if (disableBackground == null) throw new Exception("New tweak 'disable-background-apps' was not found in TweakService");
            
            var powerThrottling = tweaks.FirstOrDefault(t => t.Key == "disable-power-throttling");
            if (powerThrottling == null) throw new Exception("New tweak 'disable-power-throttling' was not found in TweakService");

            foreach (var t in tweaks)
            {
                if (string.IsNullOrEmpty(t.Key)) throw new Exception("Tweak key is null or empty");
                if (string.IsNullOrEmpty(t.Name)) throw new Exception($"Tweak {t.Key} name is null or empty");
                if (string.IsNullOrEmpty(t.Description)) throw new Exception($"Tweak {t.Key} description is null or empty");
                if (string.IsNullOrEmpty(t.Category)) throw new Exception($"Tweak {t.Key} category is null or empty");
            }
        }

        static async Task TestWindowsServices()
        {
            var services = await WindowsServicesService.GetServicesAsync();
            if (services == null || services.Count == 0) throw new Exception("No services returned by WMI");
            
            // Check critical tagging
            var rpcss = services.FirstOrDefault(s => s.Name.Equals("rpcss", StringComparison.OrdinalIgnoreCase));
            if (rpcss == null) throw new Exception("Critical service 'rpcss' not found in services list");
            if (rpcss.RiskLevel != "critical") throw new Exception($"rpcss should be marked 'critical', was marked as '{rpcss.RiskLevel}'");

            // Check bloatware tagging
            var diagtrack = services.FirstOrDefault(s => s.Name.Equals("diagtrack", StringComparison.OrdinalIgnoreCase));
            if (diagtrack != null)
            {
                if (diagtrack.RiskLevel != "bloat") throw new Exception($"diagtrack service should be marked 'bloat', was '{diagtrack.RiskLevel}'");
            }
        }

        static async Task TestDeviceCleanup()
        {
            var devices = await DeviceCleanupService.GetDisconnectedDevicesAsync();
            if (devices == null) throw new Exception("Disconnected devices list is null");
            
            foreach (var dev in devices)
            {
                if (string.IsNullOrEmpty(dev.InstanceId)) throw new Exception("Device InstanceId is null or empty");
            }
        }

        static Task TestProfileService()
        {
            var gaming = ProfileService.GetGamingPreset();
            if (gaming.ProfileName != "Perfil Gamer Máximo Rendimiento") throw new Exception("Gaming profile name invalid");
            if (!gaming.TweakStates.ContainsKey("ultimate-performance") || !gaming.TweakStates["ultimate-performance"])
                throw new Exception("Gaming preset missing ultimate-performance tweak");

            var balanced = ProfileService.GetBalancedPreset();
            if (balanced.ProfileName != "Perfil Equilibrado / Diario") throw new Exception("Balanced profile name invalid");

            var dev = ProfileService.GetDeveloperPreset();
            if (!dev.ProfileName.Contains("Desarrollador")) throw new Exception("Developer profile name invalid");
            if (!dev.TweakStates.ContainsKey("disable-superfetch") || !dev.TweakStates["disable-superfetch"])
                throw new Exception("Developer preset missing disable-superfetch tweak");
            return Task.CompletedTask;
        }

        static async Task TestDiskAnalyzerService()
        {
            string tempDir = System.IO.Path.GetTempPath();
            var items = await DiskAnalyzerService.ScanDirectoryAsync(tempDir);
            if (items == null) throw new Exception("Disk analyzer returned null list");
        }

        static async Task TestBenchmarkService()
        {
            var res = await BenchmarkService.RunBenchmarkAsync();
            if (res == null) throw new Exception("Benchmark returned null result");
            if (res.TotalScore < 50 || res.TotalScore > 1000) throw new Exception($"Invalid total score: {res.TotalScore}");
            if (string.IsNullOrEmpty(res.Grade)) throw new Exception("Grade is null or empty");
        }

        static async Task TestAntiStutterService()
        {
            SettingsService.Settings.AntiStutterEnabled = true;
            bool purged = await AntiStutterService.CheckAndPurgeIfNeededAsync();
            bool cooldown = AntiStutterService.IsCooldownActive();
            // Just verify no exception thrown
        }

        static async Task TestTelemetryService()
        {
            var status = await TelemetryService.GetStatusAsync();
            if (status == null) throw new Exception("Telemetry status returned null");
            if (status.BlockedDomainsCount <= 0) throw new Exception("Telemetry domain count invalid");
        }

        static async Task TestUninstallerService()
        {
            var apps = await UninstallerService.GetInstalledProgramsAsync();
            if (apps == null) throw new Exception("Installed apps list returned null");
        }

        static async Task TestNetworkService()
        {
            var providers = NetworkService.GetDnsProviders();
            if (providers == null || providers.Count == 0) throw new Exception("DNS providers list empty");
            var state = NetworkService.LoadNetworkState();
            if (state == null) throw new Exception("Network state returned null");
            if (await NetworkService.SetDnsAsync(new[] { "not-an-ip" }))
                throw new Exception("Network service accepted an invalid DNS address");
        }

        static Task TestAllXamlSymbolIcons()
        {
            string xamlDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "cchay-optimicer-cs");
            if (!System.IO.Directory.Exists(xamlDir)) return Task.CompletedTask;

            var files = System.IO.Directory.GetFiles(xamlDir, "*.xaml", System.IO.SearchOption.AllDirectories);
            var regex = new System.Text.RegularExpressions.Regex(@"(?:Symbol|Icon)=(?:""|)?([a-zA-Z0-9_]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

            var invalidSymbols = new List<string>();
            foreach (var file in files)
            {
                string text = System.IO.File.ReadAllText(file);
                var matches = regex.Matches(text);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string symName = match.Groups[1].Value;
                    try
                    {
                        Enum.Parse(typeof(Wpf.Ui.Controls.SymbolRegular), symName);
                    }
                    catch
                    {
                        invalidSymbols.Add($"{System.IO.Path.GetFileName(file)}: {symName}");
                    }
                }
            }

            if (invalidSymbols.Count > 0)
            {
                throw new Exception("Invalid WPF-UI Symbol names found:\n" + string.Join("\n", invalidSymbols));
            }

            return Task.CompletedTask;
        }
    }
}
