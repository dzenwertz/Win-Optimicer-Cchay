using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class GhostDevice
    {
        public string InstanceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = true;
    }

    public class DeviceCleanupService
    {
        public static Task<List<GhostDevice>> GetDisconnectedDevicesAsync()
        {
            return Task.Run(() =>
            {
                var list = new List<GhostDevice>();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = "/enum-devices /disconnected",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process != null)
                        {
                            string output = process.StandardOutput.ReadToEnd();
                            process.WaitForExit(10000);

                            var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                            GhostDevice? current = null;

                            foreach (var line in lines)
                            {
                                string trimmed = line.Trim();
                                if (trimmed.StartsWith("Instance ID:", StringComparison.OrdinalIgnoreCase) ||
                                    trimmed.StartsWith("ID de instancia:", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (current != null && !string.IsNullOrEmpty(current.InstanceId))
                                    {
                                        list.Add(current);
                                    }
                                    current = new GhostDevice
                                    {
                                        InstanceId = GetValueFromLine(line)
                                    };
                                }
                                else if (current != null)
                                {
                                    if (trimmed.StartsWith("Device Description:", StringComparison.OrdinalIgnoreCase) ||
                                        trimmed.StartsWith("Descripción del dispositivo:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        current.Description = GetValueFromLine(line);
                                    }
                                    else if (trimmed.StartsWith("Class Name:", StringComparison.OrdinalIgnoreCase) ||
                                             trimmed.StartsWith("Nombre de clase:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        current.ClassName = GetValueFromLine(line);
                                    }
                                    else if (trimmed.StartsWith("Manufacturer Name:", StringComparison.OrdinalIgnoreCase) ||
                                             trimmed.StartsWith("Nombre del fabricante:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        current.Manufacturer = GetValueFromLine(line);
                                    }
                                }
                            }

                            if (current != null && !string.IsNullOrEmpty(current.InstanceId))
                            {
                                list.Add(current);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error reading disconnected devices: " + ex.Message);
                }
                return list;
            });
        }

        private static string GetValueFromLine(string line)
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < line.Length - 1)
            {
                return line.Substring(colonIndex + 1).Trim();
            }
            return string.Empty;
        }

        public static Task<bool> RemoveDeviceAsync(string instanceId)
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/remove-device \"{instanceId}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit(5000);
                        return process?.ExitCode == 0;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
