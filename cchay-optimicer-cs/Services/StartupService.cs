using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Services
{
    public class StartupService
    {
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CchayOptimicer",
            "startupState.json"
        );

        private static bool GetIsItemEnabled(string location, string name)
        {
            try
            {
                RegistryKey? rootKey = null;
                string approvalSubKey = "";

                if (location == "HKCU Run")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                }
                else if (location == "HKCU Run (Wow6432Node)")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
                }
                else if (location == "HKLM Run")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                }
                else if (location == "HKLM Run (Wow6432Node)")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
                }
                else if (location == "User Startup Folder")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                }
                else if (location == "Common Startup Folder")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                }

                if (rootKey != null && !string.IsNullOrEmpty(approvalSubKey))
                {
                    using (var key = rootKey.OpenSubKey(approvalSubKey))
                    {
                        if (key != null)
                        {
                            var val = key.GetValue(name) as byte[];
                            if (val != null && val.Length > 0)
                            {
                                // Even first byte (like 02) = enabled, odd first byte (like 03) = disabled
                                return (val[0] & 1) == 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading StartupApproved for {name}: {ex.Message}");
            }

            // Default to true if not found in StartupApproved
            return true;
        }

        private static bool SetItemEnabledState(string location, string name, bool enabled)
        {
            try
            {
                RegistryKey? rootKey = null;
                string approvalSubKey = "";

                if (location == "HKCU Run")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                }
                else if (location == "HKCU Run (Wow6432Node)")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
                }
                else if (location == "HKLM Run")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                }
                else if (location == "HKLM Run (Wow6432Node)")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
                }
                else if (location == "User Startup Folder")
                {
                    rootKey = Registry.CurrentUser;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                }
                else if (location == "Common Startup Folder")
                {
                    rootKey = Registry.LocalMachine;
                    approvalSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
                }

                if (rootKey != null && !string.IsNullOrEmpty(approvalSubKey))
                {
                    using (var key = rootKey.OpenSubKey(approvalSubKey, writable: true))
                    {
                        if (key != null)
                        {
                            byte[] binaryVal;
                            var existing = key.GetValue(name) as byte[];
                            if (existing != null && existing.Length >= 12)
                            {
                                binaryVal = (byte[])existing.Clone();
                            }
                            else
                            {
                                binaryVal = new byte[12];
                            }

                            // 02 = enabled, 03 = disabled
                            binaryVal[0] = enabled ? (byte)0x02 : (byte)0x03;

                            if (!enabled && existing == null)
                            {
                                // Write FILETIME timestamp when disabling (bytes 4-11)
                                long fileTime = DateTime.UtcNow.ToFileTime();
                                byte[] ftBytes = BitConverter.GetBytes(fileTime);
                                Array.Copy(ftBytes, 0, binaryVal, 4, Math.Min(ftBytes.Length, 8));
                            }

                            key.SetValue(name, binaryVal, RegistryValueKind.Binary);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error writing StartupApproved for {name}: {ex.Message}");
            }
            return false;
        }

        public static Task<List<StartupItem>> GetStartupItemsAsync()
        {
            return Task.Run(() =>
            {
                var activeItems = new List<StartupItem>();

                // HKCU Run
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                var val = key.GetValue(name)?.ToString() ?? string.Empty;
                                activeItems.Add(new StartupItem
                                {
                                    Name = name,
                                    Path = val,
                                    Location = "HKCU Run",
                                    Enabled = GetIsItemEnabled("HKCU Run", name)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading HKCU run keys: {ex.Message}"); }

                // HKCU Run (Wow6432Node)
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                var val = key.GetValue(name)?.ToString() ?? string.Empty;
                                activeItems.Add(new StartupItem
                                {
                                    Name = name,
                                    Path = val,
                                    Location = "HKCU Run (Wow6432Node)",
                                    Enabled = GetIsItemEnabled("HKCU Run (Wow6432Node)", name)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading HKCU Wow6432Node run keys: {ex.Message}"); }

                // HKLM Run
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                var val = key.GetValue(name)?.ToString() ?? string.Empty;
                                activeItems.Add(new StartupItem
                                {
                                    Name = name,
                                    Path = val,
                                    Location = "HKLM Run",
                                    Enabled = GetIsItemEnabled("HKLM Run", name)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading HKLM run keys: {ex.Message}"); }

                // HKLM Run (Wow6432Node)
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (key != null)
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                var val = key.GetValue(name)?.ToString() ?? string.Empty;
                                activeItems.Add(new StartupItem
                                {
                                    Name = name,
                                    Path = val,
                                    Location = "HKLM Run (Wow6432Node)",
                                    Enabled = GetIsItemEnabled("HKLM Run (Wow6432Node)", name)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading HKLM Wow6432Node run keys: {ex.Message}"); }

                // User Startup Folder
                try
                {
                    var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
                    if (Directory.Exists(userDir))
                    {
                        // Migration: rename any .lnk.disabled back to .lnk
                        foreach (var file in Directory.GetFiles(userDir, "*.disabled"))
                        {
                            try
                            {
                                string newPath = file.Substring(0, file.Length - ".disabled".Length);
                                File.Move(file, newPath, overwrite: true);
                                SetItemEnabledState("User Startup Folder", Path.GetFileName(newPath), false);
                            }
                            catch { }
                        }

                        foreach (var file in Directory.GetFiles(userDir, "*.lnk"))
                        {
                            string name = Path.GetFileName(file);
                            activeItems.Add(new StartupItem
                            {
                                Name = name,
                                Path = file,
                                Location = "User Startup Folder",
                                Enabled = GetIsItemEnabled("User Startup Folder", name)
                            });
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading User Startup folder: {ex.Message}"); }

                // Common Startup Folder
                try
                {
                    var commonDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs\Startup");
                    if (Directory.Exists(commonDir))
                    {
                        // Migration: rename any .lnk.disabled back to .lnk
                        foreach (var file in Directory.GetFiles(commonDir, "*.disabled"))
                        {
                            try
                            {
                                string newPath = file.Substring(0, file.Length - ".disabled".Length);
                                File.Move(file, newPath, overwrite: true);
                                SetItemEnabledState("Common Startup Folder", Path.GetFileName(newPath), false);
                            }
                            catch { }
                        }

                        foreach (var file in Directory.GetFiles(commonDir, "*.lnk"))
                        {
                            string name = Path.GetFileName(file);
                            activeItems.Add(new StartupItem
                            {
                                Name = name,
                                Path = file,
                                Location = "Common Startup Folder",
                                Enabled = GetIsItemEnabled("Common Startup Folder", name)
                            });
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading Common Startup folder: {ex.Message}"); }

                // Migration: restore legacy registry disabled items
                try
                {
                    if (File.Exists(StateFilePath))
                    {
                        string json = File.ReadAllText(StateFilePath);
                        var disabledItems = JsonSerializer.Deserialize<List<StartupItem>>(json);
                        if (disabledItems != null)
                        {
                            foreach (var item in disabledItems)
                            {
                                SetItemEnabledState(item.Location, item.Name, false);
                                if (item.Location.Contains("Run"))
                                {
                                    try
                                    {
                                        var root = item.Location.StartsWith("HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
                                        string subkeyPath = item.Location.Contains("Wow6432Node")
                                            ? @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
                                            : @"Software\Microsoft\Windows\CurrentVersion\Run";
                                        using (var key = root.OpenSubKey(subkeyPath, writable: true))
                                        {
                                            if (key != null && key.GetValue(item.Name) == null)
                                            {
                                                key.SetValue(item.Name, item.Path);
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        File.Delete(StateFilePath);
                    }
                }
                catch { }

                return activeItems;
            });
        }

        public static Task<bool> ToggleStartupItemAsync(StartupItem item, bool enable)
        {
            return Task.Run(() =>
            {
                return SetItemEnabledState(item.Location, item.Name, enable);
            });
        }
    }
}
