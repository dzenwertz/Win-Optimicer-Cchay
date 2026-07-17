using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace cchay_optimicer_cs.Services
{
    public class RepairService
    {
        public static Task<bool> RunRepairCommandAsync(string commandType, Action<string> outputCallback)
        {
            return Task.Run(() =>
            {
                try
                {
                    switch (commandType)
                    {
                        case "sfc":
                            return RunProcessRedirected("sfc.exe", "/scannow", outputCallback);
                        
                        case "dism-scan":
                            return RunProcessRedirected("dism.exe", "/Online /Cleanup-Image /ScanHealth", outputCallback);
                        
                        case "dism-restore":
                            return RunProcessRedirected("dism.exe", "/Online /Cleanup-Image /RestoreHealth", outputCallback);
                        
                        case "chkdsk":
                            outputCallback("Programando CHKDSK en unidad C: para el próximo reinicio...\r\n");
                            return RunProcessRedirected("cmd.exe", "/c (echo Y & echo S) | chkdsk C: /f /r", outputCallback);
                        
                        case "winsock":
                            outputCallback("Restableciendo catálogo Winsock...\r\n");
                            return RunProcessRedirected("netsh.exe", "winsock reset", outputCallback);
                        
                        case "ipreset":
                            outputCallback("Restableciendo pila de protocolos TCP/IP...\r\n");
                            return RunProcessRedirected("netsh.exe", "int ip reset", outputCallback);
                        
                        case "flushdns":
                            outputCallback("Vaciando caché del cliente DNS...\r\n");
                            return RunProcessRedirected("ipconfig.exe", "/flushdns", outputCallback);
                        
                        case "wu-reset":
                            return ResetWindowsUpdate(outputCallback);
                        
                        case "wsreset":
                            outputCallback("Restableciendo la tienda de Windows (wsreset)...\r\n");
                            return RunProcessRedirected("wsreset.exe", "", outputCallback);
                        
                        case "iconcache":
                            return RebuildIconCache(outputCallback);
                        
                        case "permissions":
                            outputCallback("Restableciendo permisos de NTFS en la carpeta de usuario...\r\n");
                            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            return RunProcessRedirected("icacls.exe", $"\"{userPath}\" /reset /t /c /q", outputCallback);

                        default:
                            outputCallback("Comando no reconocido.\r\n");
                            return false;
                    }
                }
                catch (Exception ex)
                {
                    outputCallback($"[ERROR] Excepción: {ex.Message}\r\n");
                    return false;
                }
            });
        }

        private static bool RunProcessRedirected(string filename, string arguments, Action<string> outputCallback)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) return false;

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            outputCallback(e.Data + "\r\n");
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            outputCallback($"[ERROR] {e.Data}\r\n");
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit(600000); // 10 minutes max timeout
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                outputCallback($"[ERROR] Falló la ejecución de {filename}: {ex.Message}\r\n");
                return false;
            }
        }

        private static bool ResetWindowsUpdate(Action<string> outputCallback)
        {
            try
            {
                outputCallback("[+] Deteniendo servicios de Windows Update...\r\n");
                RunProcessRedirected("sc.exe", "stop wuauserv", outputCallback);
                RunProcessRedirected("sc.exe", "stop bits", outputCallback);
                RunProcessRedirected("sc.exe", "stop cryptsvc", outputCallback);

                outputCallback("[+] Eliminando SoftwareDistribution y Catroot2...\r\n");
                try
                {
                    string swDist = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution");
                    if (Directory.Exists(swDist))
                    {
                        Directory.Delete(swDist, true);
                        outputCallback("   -> SoftwareDistribution eliminado.\r\n");
                    }
                }
                catch (Exception ex) { outputCallback($"   [!] Omitido SoftwareDistribution: {ex.Message}\r\n"); }

                try
                {
                    string catroot2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\catroot2");
                    if (Directory.Exists(catroot2))
                    {
                        Directory.Delete(catroot2, true);
                        outputCallback("   -> Catroot2 eliminado.\r\n");
                    }
                }
                catch (Exception ex) { outputCallback($"   [!] Omitido Catroot2: {ex.Message}\r\n"); }

                outputCallback("[+] Iniciando servicios de Windows Update...\r\n");
                RunProcessRedirected("sc.exe", "start wuauserv", outputCallback);
                RunProcessRedirected("sc.exe", "start bits", outputCallback);
                RunProcessRedirected("sc.exe", "start cryptsvc", outputCallback);

                outputCallback("[OK] Windows Update restablecido correctamente.\r\n");
                return true;
            }
            catch (Exception ex)
            {
                outputCallback($"[ERROR] Error al restablecer: {ex.Message}\r\n");
                return false;
            }
        }

        private static bool RebuildIconCache(Action<string> outputCallback)
        {
            try
            {
                outputCallback("[+] Cerrando Explorer...\r\n");
                var killPsi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/f /im explorer.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(killPsi)) p?.WaitForExit(3000);

                outputCallback("[+] Eliminando caché de iconos...\r\n");
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string iconCacheDb = Path.Combine(local, "IconCache.db");
                if (File.Exists(iconCacheDb))
                {
                    try
                    {
                        File.Delete(iconCacheDb);
                        outputCallback("   -> IconCache.db eliminado.\r\n");
                    }
                    catch (Exception ex) { outputCallback($"   [!] No se pudo borrar IconCache.db: {ex.Message}\r\n"); }
                }

                string explorerCache = Path.Combine(local, @"Microsoft\Windows\Explorer");
                if (Directory.Exists(explorerCache))
                {
                    foreach (var f in Directory.GetFiles(explorerCache, "iconcache_*.db"))
                    {
                        try
                        {
                            File.Delete(f);
                        }
                        catch { }
                    }
                    outputCallback("   -> Thumbnails/Icon cache de explorer.exe limpiado.\r\n");
                }

                outputCallback("[+] Reiniciando Explorer...\r\n");
                var startPsi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c start explorer.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(startPsi)) p?.WaitForExit(1000);

                outputCallback("[OK] Caché de iconos reconstruido con éxito.\r\n");
                return true;
            }
            catch (Exception ex)
            {
                outputCallback($"[ERROR] Error al reconstruir: {ex.Message}\r\n");
                return false;
            }
        }
    }
}
