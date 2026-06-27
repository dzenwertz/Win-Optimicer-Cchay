using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class DiskService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public static List<CleanTarget> GetTargets()
        {
            return new List<CleanTarget>
            {
                new CleanTarget { Key = "temp", Name = "Archivos Temporales", Description = "Carpetas temp del usuario y sistema.", Category = "system" },
                new CleanTarget { Key = "windowsTemp", Name = "Windows Temp", Description = "C:\\Windows\\Temp", Category = "system" },
                new CleanTarget { Key = "recycleBin", Name = "Papelera de Reciclaje", Description = "Vaciar la papelera de reciclaje.", Category = "system" },
                new CleanTarget { Key = "thumbnails", Name = "Caché de Miniaturas", Description = "Thumbnails cache de Explorer.", Category = "system" },
                new CleanTarget { Key = "errorReports", Name = "Reportes de Error", Description = "Windows Error Reporting logs.", Category = "system" },
                new CleanTarget { Key = "windowsLogs", Name = "Logs de Windows", Description = "Archivos de log antiguos del sistema.", Category = "system" },
                new CleanTarget { Key = "windowsUpdate", Name = "Caché de Windows Update", Description = "Archivos descargados de actualizaciones pasadas.", Category = "windows" },
                new CleanTarget { Key = "prefetch", Name = "Prefetch", Description = "Archivos de precarga de Windows.", Category = "windows" },
                new CleanTarget { Key = "chromeCache", Name = "Chrome - Caché", Description = "Caché de Google Chrome.", Category = "browser" },
                new CleanTarget { Key = "edgeCache", Name = "Edge - Caché", Description = "Caché de Microsoft Edge.", Category = "browser" },
                new CleanTarget { Key = "firefoxCache", Name = "Firefox - Caché", Description = "Caché de Mozilla Firefox.", Category = "browser" },
                new CleanTarget { Key = "braveCache", Name = "Brave - Caché", Description = "Caché de Brave Browser.", Category = "browser" },
                new CleanTarget { Key = "deliveryOptimization", Name = "Caché de Optimización de Entrega", Description = "Archivos de actualizaciones de Windows compartidos en red P2P.", Category = "windows" },
                new CleanTarget { Key = "shaderCache", Name = "Caché de Sombreadores DirectX/GPUs", Description = "Caché de texturas y sombreadores gráficos (DirectX, NVIDIA, AMD).", Category = "system" },
                new CleanTarget { Key = "windowsUpdateReset", Name = "Limpieza de Componentes Windows", Description = "Libera espacio de actualizaciones viejas de Windows usando DISM. Puede tardar unos minutos.", Category = "windows" },
                new CleanTarget { Key = "defragTrim", Name = "Optimización de Unidad (TRIM/Defrag)", Description = "Ejecuta TRIM en SSD o desfragmentación en HDD para la unidad C:.", Category = "system" }
            };
        }

        private static List<string> GetPathsForTarget(string key)
        {
            var paths = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            switch (key)
            {
                case "temp":
                    paths.Add(Path.GetTempPath());
                    break;
                case "windowsTemp":
                    paths.Add(@"C:\Windows\Temp");
                    break;
                case "thumbnails":
                    paths.Add(Path.Combine(local, @"Microsoft\Windows\Explorer"));
                    break;
                case "errorReports":
                    paths.Add(Path.Combine(local, @"Microsoft\Windows\WER"));
                    paths.Add(@"C:\ProgramData\Microsoft\Windows\WER");
                    break;
                case "windowsLogs":
                    paths.Add(@"C:\Windows\Logs");
                    paths.Add(@"C:\Windows\Panther");
                    break;
                case "windowsUpdate":
                    paths.Add(@"C:\Windows\SoftwareDistribution\Download");
                    break;
                case "prefetch":
                    paths.Add(@"C:\Windows\Prefetch");
                    break;
                case "chromeCache":
                    string chromeBase = Path.Combine(local, @"Google\Chrome\User Data");
                    AddChromiumCachePaths(chromeBase, paths);
                    break;
                case "edgeCache":
                    string edgeBase = Path.Combine(local, @"Microsoft\Edge\User Data");
                    AddChromiumCachePaths(edgeBase, paths);
                    break;
                case "braveCache":
                    string braveBase = Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data");
                    AddChromiumCachePaths(braveBase, paths);
                    break;
                case "firefoxCache":
                    string ffBase = Path.Combine(local, @"Mozilla\Firefox\Profiles");
                    if (Directory.Exists(ffBase))
                    {
                        foreach (var profile in Directory.GetDirectories(ffBase))
                        {
                            paths.Add(Path.Combine(profile, "cache2"));
                        }
                    }
                    break;
                case "deliveryOptimization":
                    paths.Add(@"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache");
                    break;
                case "shaderCache":
                    paths.Add(Path.Combine(local, @"D3DSCache"));
                    paths.Add(Path.Combine(local, @"Microsoft\DirectX Shader Cache"));
                    paths.Add(Path.Combine(local, @"NVIDIA\GLCache"));
                    paths.Add(Path.Combine(local, @"NVIDIA Corporation\NV_Cache"));
                    paths.Add(Path.Combine(local, @"AMD\DxCache"));
                    break;
            }
            return paths;
        }

        private static void AddChromiumCachePaths(string basePath, List<string> paths)
        {
            if (!Directory.Exists(basePath)) return;
            string[] subdirs = { 
                @"Default\Cache", @"Default\Code Cache", @"Default\GPUCache", 
                @"ShaderCache", @"Default\Service Worker\CacheStorage", @"GrShaderCache\GPUCache" 
            };
            foreach (var d in subdirs)
            {
                paths.Add(Path.Combine(basePath, d));
            }
        }

        private static void CleanDirectory(string path, ref double freedMB, ref int itemsCount, bool delete, string filter = "*")
        {
            if (!Directory.Exists(path)) return;

            // Clean files in current directory
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles(filter))
                {
                    try
                    {
                        freedMB += file.Length / 1024.0 / 1024.0;
                        itemsCount++;
                        if (delete)
                        {
                            file.Delete();
                        }
                    }
                    catch { /* Locked, ignore */ }
                }
            }
            catch { /* Access denied to current directory files, ignore */ }

            // Recurse into subdirectories
            try
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    CleanDirectory(subDir.FullName, ref freedMB, ref itemsCount, delete, filter);
                    if (delete)
                    {
                        try
                        {
                            // If empty, delete it
                            if (Directory.GetFileSystemEntries(subDir.FullName).Length == 0)
                            {
                                subDir.Delete();
                            }
                        }
                        catch { /* Directory in use or locked */ }
                    }
                }
            }
            catch { /* Access denied to subdirectories, ignore */ }
        }

        public static Task<CleanTarget> ProcessTargetAsync(CleanTarget target, bool delete)
        {
            return Task.Run(() =>
            {
                double freedMB = 0;
                int itemsCount = 0;

                if (target.Key == "recycleBin")
                {
                    // For Recycle Bin size, we could query Shell APIs, but for deletion we just call SHEmptyRecycleBin
                    if (delete)
                    {
                        try
                        {
                            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                            target.Cleaned = true;
                        }
                        catch { }
                    }
                    else
                    {
                        // Set size to 0 for preview since calculating recycle bin size natively in C# requires complex Shell COM interfaces
                        target.SizeMB = 0;
                    }
                }
                else if (target.Key == "windowsUpdateReset")
                {
                    if (delete)
                    {
                        try
                        {
                            var dismPsi = new ProcessStartInfo
                            {
                                FileName = "dism.exe",
                                Arguments = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            var dismProc = Process.Start(dismPsi);
                            dismProc?.WaitForExit(300000); // up to 5 min
                            target.Cleaned = true;
                        }
                        catch { }
                    }
                    else
                    {
                        target.SizeMB = 0;
                    }
                }
                else if (target.Key == "defragTrim")
                {
                    if (delete)
                    {
                        try
                        {
                            var defragPsi = new ProcessStartInfo
                            {
                                FileName = "defrag.exe",
                                Arguments = "C: /O",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            var defragProc = Process.Start(defragPsi);
                            defragProc?.WaitForExit(60000);
                            target.Cleaned = true;
                        }
                        catch { }
                    }
                    else
                    {
                        target.SizeMB = 0;
                    }
                }
                else
                {
                    var paths = GetPathsForTarget(target.Key);
                    string filter = target.Key == "thumbnails" ? "thumbcache_*" : "*";
                    
                    foreach (var path in paths)
                    {
                        CleanDirectory(path, ref freedMB, ref itemsCount, delete, filter);
                    }
                    
                    target.SizeMB = Math.Round(freedMB, 2);
                    target.ItemsCount = itemsCount;
                    target.Cleaned = delete;
                }

                return target;
            });
        }
    }
}
