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
    }
}
