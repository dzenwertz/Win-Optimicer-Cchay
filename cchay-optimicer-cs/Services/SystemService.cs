using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
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
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
                            break;
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
                    {
                        var gpus = searcher.Get().Cast<ManagementObject>()
                            .Select(o => o["Name"]?.ToString() ?? "")
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList();
                        if (gpus.Count > 0)
                        {
                            // Prefer dedicated GPU over integrated if possible
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
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.OsName = obj["Caption"]?.ToString() ?? "Windows";
                            break;
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

                return info;
            });
        }
    }
}
