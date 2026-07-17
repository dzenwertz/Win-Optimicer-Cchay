using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class SystemInfoData
    {
        public string CpuModel { get; set; } = "Unknown Processor";
        public int CpuCores { get; set; } = 1;
        public int CpuThreads { get; set; } = 1;
        public ulong MemoryTotal { get; set; }
        public ulong MemoryUsed { get; set; }
        public ulong MemoryFree { get; set; }
        public int MemoryPercent { get; set; }
        public string OsName { get; set; } = "Windows";
        public string OsVersion { get; set; } = "Unknown";
        public string OsBuild { get; set; } = "Unknown";
        public string GpuModel { get; set; } = "Detecting...";
        public long DiskTotal { get; set; }
        public long DiskUsed { get; set; }
        public long DiskFree { get; set; }
        public int DiskPercent { get; set; }
        public string Username { get; set; } = "Usuario";
        public bool IsAdmin { get; set; }
        public string CpuTemperature { get; set; } = "N/A";
        public string SmartStatus { get; set; } = "N/A";
        public string LocalIp { get; set; } = "N/A";
        public string PublicIp { get; set; } = "N/A";
        public string Uptime { get; set; } = "N/A";
        public int HealthScore { get; set; } = 100;
    }

    public class SystemService
    {
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static Task<SystemInfoData> GetSystemInfoAsync()
        {
            return Task.Run(() =>
            {
                var info = new SystemInfoData();

                // Username
                info.Username = Environment.UserName;
                info.IsAdmin = IsRunningAsAdmin();

                // Memory Info
                var mem = RamService.GetMemoryUsage();
                info.MemoryTotal = mem.TotalBytes;
                info.MemoryUsed = mem.UsedBytes;
                info.MemoryFree = mem.FreeBytes;
                info.MemoryPercent = mem.PercentUsed;

                // CPU Model via Registry (fastest & most reliable)
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (key != null)
                        {
                            info.CpuModel = key.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown CPU";
                        }
                    }
                }
                catch { /* ignore */ }

                // CPU Cores & Threads
                info.CpuThreads = Environment.ProcessorCount;
                try
                {
                    using (var searcher = new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor"))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    info.CpuCores = Environment.ProcessorCount / 2;
                    if (info.CpuCores < 1) info.CpuCores = 1;
                }

                // GPU Model
                try
                {
                    using (var searcher = new ManagementObjectSearcher("Select Name from Win32_VideoController"))
                    using (var results = searcher.Get())
                    {
                        var gpus = new List<string>();
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                string name = obj["Name"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(name)) gpus.Add(name);
                            }
                        }
                        if (gpus.Count > 0)
                        {
                            var dedicated = gpus.FirstOrDefault(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || 
                                                                     g.Contains("AMD", StringComparison.OrdinalIgnoreCase) || 
                                                                     g.Contains("Radeon", StringComparison.OrdinalIgnoreCase) || 
                                                                     g.Contains("GeForce", StringComparison.OrdinalIgnoreCase));
                            info.GpuModel = dedicated ?? gpus[0];
                        }
                        else
                        {
                            info.GpuModel = "Microsoft Basic Display Adapter";
                        }
                    }
                }
                catch
                {
                    info.GpuModel = "Unknown GPU";
                }

                // OS Info
                try
                {
                    using (var searcher = new ManagementObjectSearcher("Select Caption from Win32_OperatingSystem"))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                info.OsName = obj["Caption"]?.ToString() ?? "Windows";
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    info.OsName = "Windows " + (Environment.OSVersion.Version.Major == 10 ? (Environment.OSVersion.Version.Build >= 22000 ? "11" : "10") : "10");
                }

                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (key != null)
                        {
                            info.OsVersion = key.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                            info.OsBuild = key.GetValue("CurrentBuildNumber")?.ToString() ?? "Unknown";
                        }
                    }
                }
                catch { /* ignore */ }

                // Disk Info (Drive C:)
                try
                {
                    var drive = new DriveInfo("C");
                    if (drive.IsReady)
                    {
                        info.DiskTotal = drive.TotalSize;
                        info.DiskFree = drive.AvailableFreeSpace;
                        info.DiskUsed = info.DiskTotal - info.DiskFree;
                        info.DiskPercent = info.DiskTotal > 0 ? (int)((double)info.DiskUsed / info.DiskTotal * 100) : 0;
                    }
                }
                catch { /* ignore */ }

                // CPU Temperature
                info.CpuTemperature = GetCpuTemperature();

                // SMART Status
                info.SmartStatus = GetSmartStatus();

                // Uptime
                info.Uptime = GetSystemUptime();

                // Local IP
                info.LocalIp = GetLocalIp();

                // Public IP
                info.PublicIp = GetPublicIp();

                // Calculate Health Score
                info.HealthScore = CalculateHealthScore(info);

                return info;
            });
        }

        private static string GetCpuTemperature()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            double tempRaw = Convert.ToDouble(obj["CurrentTemperature"]);
                            double tempCelsius = (tempRaw / 10.0) - 273.15;
                            if (tempCelsius < 0 || tempCelsius > 150) continue; // skip invalid readings
                            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1} °C", tempCelsius);
                        }
                    }
                }
            }
            catch { }
            return "N/A / Bloqueado";
        }

        private static string GetSmartStatus()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT OperationalStatus FROM MSFT_PhysicalDisk"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            var statusArr = obj["OperationalStatus"] as ushort[];
                            if (statusArr != null && statusArr.Length > 0)
                            {
                                if (statusArr[0] == 2) return "Saludable";
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT PredictFailure FROM MSStorageDriver_FailurePredictStatus"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            bool fail = Convert.ToBoolean(obj["PredictFailure"]);
                            return fail ? "Alerta (Posible Fallo)" : "Saludable";
                        }
                    }
                }
            }
            catch { }
            return "Desconocido";
        }

        private static string GetSystemUptime()
        {
            try
            {
                long tickCount = Environment.TickCount64;
                TimeSpan ts = TimeSpan.FromMilliseconds(tickCount);
                if (ts.TotalDays >= 1)
                    return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
            catch
            {
                return "N/A";
            }
        }

        private static string GetLocalIp()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private static string GetPublicIp()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(1500);
                    return client.GetStringAsync("https://api.ipify.org").Result.Trim();
                }
            }
            catch
            {
                return "Desconectado";
            }
        }

        public static int CalculateHealthScore(SystemInfoData info)
        {
            int score = 100;

            // RAM usage
            if (info.MemoryPercent > 90) score -= 25;
            else if (info.MemoryPercent > 80) score -= 15;
            else if (info.MemoryPercent > 70) score -= 5;

            // Disk usage
            if (info.DiskPercent > 90) score -= 20;
            else if (info.DiskPercent > 80) score -= 10;

            // CPU Temp
            if (info.CpuTemperature != "N/A" && info.CpuTemperature != "N/A / Bloqueado")
            {
                string rawTemp = info.CpuTemperature.Replace(" °C", "").Trim();
                if (double.TryParse(rawTemp, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temp))
                {
                    if (temp > 85) score -= 20;
                    else if (temp > 75) score -= 10;
                }
            }

            if (score < 0) score = 0;
            return score;
        }

        public static string GenerateSystemReport(SystemInfoData info)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("         CCHAY OPTIMICER - REPORTE DE PC          ");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Fecha/Hora:       {DateTime.Now}");
            sb.AppendLine($"Usuario:          {info.Username}");
            sb.AppendLine($"Ejecutando Admin: {info.IsAdmin}");
            sb.AppendLine($"Puntaje de Salud: {info.HealthScore}/100");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("INFORMACIÓN DE HARDWARE & SISTEMA:");
            sb.AppendLine($"SO:               {info.OsName} {info.OsVersion} (Build {info.OsBuild})");
            sb.AppendLine($"CPU:              {info.CpuModel}");
            sb.AppendLine($"Núcleos/Hilos:    {info.CpuCores} Cores / {info.CpuThreads} Threads");
            sb.AppendLine($"Temperatura CPU:  {info.CpuTemperature}");
            sb.AppendLine($"GPU:              {info.GpuModel}");
            sb.AppendLine($"RAM Total:        {(info.MemoryTotal / 1024.0 / 1024.0 / 1024.0):F2} GB");
            sb.AppendLine($"RAM Usada:        {(info.MemoryUsed / 1024.0 / 1024.0 / 1024.0):F2} GB ({info.MemoryPercent}%)");
            sb.AppendLine($"RAM Libre:        {(info.MemoryFree / 1024.0 / 1024.0 / 1024.0):F2} GB");
            sb.AppendLine($"Disco C: Total:   {(info.DiskTotal / 1024.0 / 1024.0 / 1024.0):F1} GB");
            sb.AppendLine($"Disco C: Usado:   {(info.DiskUsed / 1024.0 / 1024.0 / 1024.0):F1} GB ({info.DiskPercent}%)");
            sb.AppendLine($"Disco C: Libre:   {(info.DiskFree / 1024.0 / 1024.0 / 1024.0):F1} GB");
            sb.AppendLine($"Estado SMART:     {info.SmartStatus}");
            sb.AppendLine($"IP Local:         {info.LocalIp}");
            sb.AppendLine($"IP Pública:       {info.PublicIp}");
            sb.AppendLine($"Uptime:           {info.Uptime}");
            sb.AppendLine("==================================================");
            return sb.ToString();
        }
    }
}
