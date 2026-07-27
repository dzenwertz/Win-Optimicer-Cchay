using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace cchay_optimicer_cs.Services
{
    public class InstalledProgram
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Version { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public string QuietUninstallString { get; set; } = "";
        public string InstallLocation { get; set; } = "";
    }

    public class OrphanResidue
    {
        public string PathOrKey { get; set; } = "";
        public string Type { get; set; } = "Folder"; // "Folder" or "RegistryKey"
        public string Description { get; set; } = "";
    }

    public class UninstallerService
    {
        public static Task<List<InstalledProgram>> GetInstalledProgramsAsync()
        {
            return Task.Run(() =>
            {
                var list = new List<InstalledProgram>();
                var keys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                // Read HKLM and HKCU
                foreach (var keyPath in keys)
                {
                    ReadUninstallKey(Registry.LocalMachine, keyPath, list);
                    ReadUninstallKey(Registry.CurrentUser, keyPath, list);
                }

                return list.OrderBy(p => p.Name).ToList();
            });
        }

        private static void ReadUninstallKey(RegistryKey rootKey, string subKeyPath, List<InstalledProgram> list)
        {
            try
            {
                using var key = rootKey.OpenSubKey(subKeyPath);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subName);
                        if (subKey == null) continue;

                        string displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                        string uninstallString = subKey.GetValue("UninstallString")?.ToString() ?? "";
                        int systemComponent = Convert.ToInt32(subKey.GetValue("SystemComponent") ?? 0);

                        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(uninstallString) || systemComponent == 1)
                            continue;

                        // Avoid duplicates
                        if (list.Any(p => p.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        list.Add(new InstalledProgram
                        {
                            Name = displayName,
                            Publisher = subKey.GetValue("Publisher")?.ToString() ?? "Desconocido",
                            Version = subKey.GetValue("DisplayVersion")?.ToString() ?? "",
                            InstallDate = subKey.GetValue("InstallDate")?.ToString() ?? "",
                            UninstallString = uninstallString,
                            QuietUninstallString = subKey.GetValue("QuietUninstallString")?.ToString() ?? "",
                            InstallLocation = subKey.GetValue("InstallLocation")?.ToString() ?? ""
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static Task<bool> UninstallProgramAsync(InstalledProgram program)
        {
            return Task.Run(() =>
            {
                string cmd = !string.IsNullOrEmpty(program.QuietUninstallString)
                    ? program.QuietUninstallString
                    : program.UninstallString;

                if (string.IsNullOrWhiteSpace(cmd)) return false;

                try
                {
                    ProcessStartInfo psi;
                    if (cmd.StartsWith("\""))
                    {
                        int idx = cmd.IndexOf("\"", 1);
                        if (idx <= 1) return false;
                        string fileName = cmd.Substring(1, idx - 1);
                        string args = cmd.Substring(idx + 1).Trim();
                        psi = new ProcessStartInfo(fileName, args);
                    }
                    else
                    {
                        int executableEnd = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                        if (executableEnd >= 0)
                        {
                            executableEnd += 4;
                            string fileName = cmd[..executableEnd].Trim();
                            string args = cmd[executableEnd..].Trim();
                            psi = new ProcessStartInfo(fileName, args);
                        }
                        else
                        {
                            string[] parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 0) return false;
                            psi = new ProcessStartInfo(parts[0], parts.Length > 1 ? parts[1] : "");
                        }
                    }

                    psi.UseShellExecute = true;
                    var proc = Process.Start(psi);
                    return proc != null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UninstallerService] Error uninstalling {program.Name}: {ex.Message}");
                    return false;
                }
            });
        }

        public static Task<List<OrphanResidue>> ScanOrphanResiduesAsync(InstalledProgram program)
        {
            return Task.Run(() =>
            {
                var residues = new List<OrphanResidue>();
                string name = program.Name;
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3) return residues;

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                // Scan folders
                string[] searchDirs = { local, appData, progData };
                foreach (var dir in searchDirs)
                {
                    try
                    {
                        if (!Directory.Exists(dir)) continue;
                        foreach (var sub in Directory.GetDirectories(dir))
                        {
                            string folderName = Path.GetFileName(sub);
                            if (folderName.Contains(name, StringComparison.OrdinalIgnoreCase))
                            {
                                residues.Add(new OrphanResidue
                                {
                                    PathOrKey = sub,
                                    Type = "Carpeta",
                                    Description = $"Carpeta residual en AppData ({folderName})"
                                });
                            }
                        }
                    }
                    catch { }
                }

                return residues;
            });
        }

        public static Task CleanResiduesAsync(List<OrphanResidue> residues)
        {
            return Task.Run(() =>
            {
                foreach (var res in residues)
                {
                    try
                    {
                        if (res.Type == "Carpeta" && Directory.Exists(res.PathOrKey))
                        {
                            Directory.Delete(res.PathOrKey, true);
                        }
                    }
                    catch { }
                }
            });
        }
    }
}
