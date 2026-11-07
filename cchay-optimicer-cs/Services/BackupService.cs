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

        private const string SystemRestoreKey = @"Software\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyValue = "SystemRestorePointCreationFrequency";

        /// <summary>
        /// Crea un punto de restauración completo (BEGIN + END). Quita temporalmente el
        /// límite de un punto cada 24 h y deja el registro como estaba.
        /// </summary>
        public static Task<bool> CreateRestorePointAsync(string description)
        {
            return Task.Run(() =>
            {
                object? previousFrequency = null;
                bool frequencyChanged = false;
                try
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey(SystemRestoreKey, true);
                    if (key != null)
                    {
                        previousFrequency = key.GetValue(FrequencyValue);
                        key.SetValue(FrequencyValue, 0, RegistryValueKind.DWord);
                        frequencyChanged = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Could not lift restore point frequency limit: " + ex.Message);
                }

                try
                {
                    var begin = new RESTOREPOINTINFO
                    {
                        dwEventType = BEGIN_SYSTEM_CHANGE,
                        dwRestorePtType = MODIFY_SETTINGS,
                        llSequenceNumber = 0,
                        szDescription = string.IsNullOrEmpty(description) ? "RethOptimicer_Backup" : $"RethOptimicer_{description}"
                    };

                    if (!SRSetRestorePoint(ref begin, out var status) || status.nStatus != 0)
                        return false;

                    // Sin END_SYSTEM_CHANGE el punto queda incompleto y Windows puede descartarlo.
                    var end = new RESTOREPOINTINFO
                    {
                        dwEventType = END_SYSTEM_CHANGE,
                        dwRestorePtType = MODIFY_SETTINGS,
                        llSequenceNumber = status.llSequenceNumber,
                        szDescription = string.Empty
                    };
                    return SRSetRestorePoint(ref end, out var endStatus) && endStatus.nStatus == 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to create restore point: " + ex.Message);
                    return false;
                }
                finally
                {
                    if (frequencyChanged)
                        RestoreFrequencyValue(previousFrequency);
                }
            });
        }

        private static void RestoreFrequencyValue(object? previous)
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(SystemRestoreKey, true);
                if (key == null) return;
                if (previous == null)
                    key.DeleteValue(FrequencyValue, false);
                else
                    key.SetValue(FrequencyValue, previous, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not restore frequency value: " + ex.Message);
            }
        }

        /// <summary>
        /// Llamar antes de cualquier cambio en el sistema. Devuelve true si se puede seguir:
        /// el punto se creó, el usuario desactivó los puntos automáticos, o aceptó continuar
        /// sin respaldo. Debe llamarse desde el hilo de la interfaz.
        /// </summary>
        public static async Task<bool> EnsureRestorePointAsync(string description)
        {
            if (!SettingsService.Settings.AutoRestorePointEnabled)
                return true;

            if (await CreateRestorePointAsync(description))
                return true;

            var answer = System.Windows.MessageBox.Show(
                "No se pudo crear un punto de restauración, así que este cambio no se podría deshacer desde Windows.\n\n" +
                "Suele pasar cuando la Protección del sistema está desactivada en C: " +
                "(Panel de control › Sistema › Protección del sistema).\n\n" +
                "¿Aplicar el cambio de todas formas, sin respaldo?",
                "RETH Optimicer · Sin punto de restauración",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);
            return answer == System.Windows.MessageBoxResult.Yes;
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
                    // Only System Restore on C:, not every VSS snapshot (File History, backups).
                    var result = ProcessRunner.RunPowerShell(
                        "Disable-ComputerRestore -Drive 'C:\\'; Enable-ComputerRestore -Drive 'C:\\'",
                        60000);
                    return result.Success;
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
