using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class BackupService
    {
        // --- Win32 System Restore API P/Invoke ---

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RESTOREPOINTINFO
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STATEMGRSTATUS
        {
            public int nStatus;
            public long llSequenceNumber;
        }

        [DllImport("srclient.dll", CharSet = CharSet.Unicode)]
        private static extern bool SRSetRestorePoint(ref RESTOREPOINTINFO pRestorePtSpec, out STATEMGRSTATUS pStatus);

        // Event types
        private const int BEGIN_SYSTEM_CHANGE = 100;
        private const int END_SYSTEM_CHANGE = 101;

        // Restore point types
        private const int APPLICATION_INSTALL = 0;
        private const int APPLICATION_UNINSTALL = 1;
        private const int DEVICE_DRIVER_INSTALL = 10;
        private const int MODIFY_SETTINGS = 12;

        public static Task<bool> CreateRestorePointAsync(string description)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Disable 24-hour restore point cooldown interval by setting Registry key
                    using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\SystemRestore", true))
                    {
                        key?.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
                    }
                }
                catch { /* Ignore if permission fails, although we run as admin */ }

                try
                {
                    var info = new RESTOREPOINTINFO
                    {
                        dwEventType = BEGIN_SYSTEM_CHANGE,
                        dwRestorePtType = MODIFY_SETTINGS,
                        llSequenceNumber = 0,
                        szDescription = string.IsNullOrEmpty(description) ? "CchayOptimicer_Backup" : $"CchayOptimicer_{description}"
                    };

                    var status = new STATEMGRSTATUS();
                    bool success = SRSetRestorePoint(ref info, out status);
                    return success && status.nStatus == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to create restore point: " + ex.Message);
                    return false;
                }
            });
        }

        public static Task<List<RestorePoint>> GetRestorePointsAsync()
        {
            return Task.Run(() =>
            {
                var list = new List<RestorePoint>();
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\default", "SELECT * FROM SystemRestore"))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                var creationTimeStr = obj["CreationTime"]?.ToString() ?? "";
                                string dateStr = creationTimeStr;
                                if (creationTimeStr.Length >= 14)
                                {
                                    string year = creationTimeStr.Substring(0, 4);
                                    string month = creationTimeStr.Substring(4, 2);
                                    string day = creationTimeStr.Substring(6, 2);
                                    string hour = creationTimeStr.Substring(8, 2);
                                    string minute = creationTimeStr.Substring(10, 2);
                                    string second = creationTimeStr.Substring(12, 2);
                                    dateStr = $"{year}-{month}-{day} {hour}:{minute}:{second}";
                                }
                                
                                list.Add(new RestorePoint
                                {
                                    SequenceNumber = obj["SequenceNumber"] != null ? Convert.ToUInt32(obj["SequenceNumber"]) : 0,
                                    Description = obj["Description"]?.ToString() ?? "",
                                    CreationTime = dateStr
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error listing restore points: " + ex.Message);
                }
                return list;
            });
        }

        public static Task<bool> DeleteAllRestorePointsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = "delete shadows /all /quiet",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var process = Process.Start(psi);
                    process?.WaitForExit(10000);
                    return process?.ExitCode == 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static void OpenSystemRestoreUI()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rstrui.exe",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open rstrui.exe: " + ex.Message);
            }
        }
    }
}
