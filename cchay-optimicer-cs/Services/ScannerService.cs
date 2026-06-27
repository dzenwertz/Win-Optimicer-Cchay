using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class ScannerService
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, MoveFileFlags dwFlags);

        [Flags]
        private enum MoveFileFlags : uint
        {
            MOVEFILE_REPLACE_EXISTING = 0x00000001,
            MOVEFILE_COPY_ALLOWED = 0x00000002,
            MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004,
            MOVEFILE_WRITE_THROUGH = 0x00000008,
            MOVEFILE_CREATE_HARDLINK = 0x00000010,
            MOVEFILE_FAIL_IF_NOT_TRACKABLE = 0x00000020
        }

        private static bool IsFileSigned(string filePath)
        {
            try
            {
                using (var cert = new X509Certificate2(filePath))
                {
                    return cert != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static Task<List<ThreatItem>> ScanRunningProcessesAsync(Action<string, int>? progressCallback)
        {
            return Task.Run(() =>
            {
                var threats = new List<ThreatItem>();
                var processes = Process.GetProcesses();
                int total = processes.Length;
                int current = 0;

                string tempPath = Path.GetTempPath();
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                foreach (var p in processes)
                {
                    current++;
                    try
                    {
                        if (p.Id == 0 || p.Id == 4 || p.Id == Process.GetCurrentProcess().Id) continue; // Idle, System, and Self

                        string name = p.ProcessName;
                        progressCallback?.Invoke(name, (int)((double)current / total * 100));

                        string? filePath = null;
                        try
                        {
                            filePath = p.MainModule?.FileName;
                        }
                        catch
                        {
                            // Access Denied occurs on system critical processes (svchost, csrss, lsass, etc.)
                            // These are protected by Windows kernel and are safe
                            continue;
                        }

                        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) continue;

                        // Rule A: Runs from a temporary folder
                        bool isFromTemp = filePath.Contains(tempPath, StringComparison.OrdinalIgnoreCase) ||
                                          filePath.Contains(@"\AppData\Local\Temp", StringComparison.OrdinalIgnoreCase);

                        // Rule B: Unsigned and running from user directories (AppData, userProfile, Downloads, etc.)
                        bool isFromUserDir = filePath.Contains(userProfile, StringComparison.OrdinalIgnoreCase) &&
                                             !filePath.Contains(@"\Program Files", StringComparison.OrdinalIgnoreCase) &&
                                             !filePath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase);

                        if (isFromTemp)
                        {
                            bool signed = IsFileSigned(filePath);
                            if (!signed)
                            {
                                threats.Add(new ThreatItem
                                {
                                    Name = name,
                                    Path = filePath,
                                    Reason = "Ejecutando desde carpeta temporal sin firma digital.",
                                    RiskLevel = "Alto",
                                    ProcessId = p.Id
                                });
                            }
                            else
                            {
                                threats.Add(new ThreatItem
                                {
                                    Name = name,
                                    Path = filePath,
                                    Reason = "Proceso ejecutándose desde directorio temporal (posible instalador o adware).",
                                    RiskLevel = "Medio",
                                    ProcessId = p.Id
                                });
                            }
                        }
                        else if (isFromUserDir)
                        {
                            bool signed = IsFileSigned(filePath);
                            if (!signed)
                            {
                                bool isDeveloperTool = filePath.Contains(@"\.vscode\", StringComparison.OrdinalIgnoreCase) || 
                                                       filePath.Contains(@"\.git\", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\AppData\Local\Programs\Microsoft VS Code", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\AppData\Local\Programs\Antigravity IDE", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\antigravity", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\npm", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\Visual Studio", StringComparison.OrdinalIgnoreCase) ||
                                                       filePath.Contains(@"\JetBrains", StringComparison.OrdinalIgnoreCase);

                                if (!isDeveloperTool)
                                {
                                    threats.Add(new ThreatItem
                                    {
                                        Name = name,
                                        Path = filePath,
                                        Reason = "Ejecutable no firmado ejecutándose desde la carpeta de usuario o AppData.",
                                        RiskLevel = "Alto",
                                        ProcessId = p.Id
                                    });
                                }
                            }
                        }

                        // Rule C: Process Masquerading (e.g. svchost.exe or lsass.exe not running from System32)
                        string fileName = Path.GetFileName(filePath);
                        bool isSystemName = fileName.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals("lsass.exe", StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals("csrss.exe", StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals("winlogon.exe", StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals("services.exe", StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals("smss.exe", StringComparison.OrdinalIgnoreCase);

                        if (isSystemName)
                        {
                            bool isSystemPath = filePath.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase) ||
                                                filePath.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase);
                            if (!isSystemPath)
                            {
                                threats.Add(new ThreatItem
                                {
                                    Name = name,
                                    Path = filePath,
                                    Reason = $"Suplantación del proceso del sistema. Se ejecuta desde la ruta inusual: {filePath}",
                                    RiskLevel = "Crítico",
                                    ProcessId = p.Id
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual failures
                    }
                }

                return threats;
            });
        }

        public static Task<List<ThreatItem>> ScanPathWithDefenderAsync(string targetPath)
        {
            return Task.Run(() =>
            {
                var threats = new List<ThreatItem>();
                if (!File.Exists(targetPath) && !Directory.Exists(targetPath)) return threats;

                // Find Defender CLI path
                string defenderPath = @"C:\Program Files\Windows Defender\MpCmdRun.exe";
                if (!File.Exists(defenderPath))
                {
                    defenderPath = @"C:\Program Files (x86)\Windows Defender\MpCmdRun.exe";
                }

                if (!File.Exists(defenderPath)) return threats; // Defender not found

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = defenderPath,
                        Arguments = $"-Scan -ScanType 3 -File \"{targetPath}\" -DisableRemediation",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            string output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(60000); // 1 minute timeout

                            bool threatFound = proc.ExitCode == 2 || 
                                               output.Contains("threat", StringComparison.OrdinalIgnoreCase) ||
                                               output.Contains("infection", StringComparison.OrdinalIgnoreCase);

                            if (threatFound)
                            {
                                threats.Add(new ThreatItem
                                {
                                    Name = Path.GetFileName(targetPath),
                                    Path = targetPath,
                                    Reason = "Virus/Amenaza detectada por Microsoft Defender.",
                                    RiskLevel = "Crítico",
                                    ProcessId = 0
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Defender scan error: {ex.Message}");
                }

                return threats;
            });
        }

        public static Task<bool> KillAndCleanThreatAsync(ThreatItem threat)
        {
            return Task.Run(() =>
            {
                bool processKilled = true;
                
                // Kill process if active
                if (threat.ProcessId > 0)
                {
                    try
                    {
                        var proc = Process.GetProcessById(threat.ProcessId);
                        proc.Kill(true); // Kill process tree
                        proc.WaitForExit(3000);
                    }
                    catch (ArgumentException)
                    {
                        // Process already terminated
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to kill process: {ex.Message}");
                        processKilled = false;
                    }
                }

                if (!processKilled) return false;

                // Delete file
                try
                {
                    if (File.Exists(threat.Path))
                    {
                        File.SetAttributes(threat.Path, FileAttributes.Normal);
                        File.Delete(threat.Path);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete threat file: {ex.Message}");
                    
                    // Delay until reboot
                    try
                    {
                        return MoveFileEx(threat.Path, null, MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT);
                    }
                    catch
                    {
                        return false;
                    }
                }
            });
        }
    }
}
