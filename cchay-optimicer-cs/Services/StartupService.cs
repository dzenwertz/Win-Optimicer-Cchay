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

        private static List<StartupItem> LoadDisabledItems()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    return JsonSerializer.Deserialize<List<StartupItem>>(json) ?? new List<StartupItem>();
                }
            }
            catch { /* ignore */ }
            return new List<StartupItem>();
        }

        private static void SaveDisabledItems(List<StartupItem> items)
        {
            try
            {
                string dir = Path.GetDirectoryName(StateFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save disabled items: {ex.Message}");
            }
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
                                    Enabled = true
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
                                    Enabled = true
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
                                    Enabled = true
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
                                    Enabled = true
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
                        foreach (var file in Directory.GetFiles(userDir, "*.lnk"))
                        {
                            activeItems.Add(new StartupItem
                            {
                                Name = Path.GetFileName(file),
                                Path = file,
                                Location = "User Startup Folder",
                                Enabled = true
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
                        foreach (var file in Directory.GetFiles(commonDir, "*.lnk"))
                        {
                            activeItems.Add(new StartupItem
                            {
                                Name = Path.GetFileName(file),
                                Path = file,
                                Location = "Common Startup Folder",
                                Enabled = true
                            });
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error reading Common Startup folder: {ex.Message}"); }

                var disabled = LoadDisabledItems();

                // Merge and return: only add disabled items that are not currently active
                var result = new List<StartupItem>();
                result.AddRange(activeItems);

                var cleanedDisabled = new List<StartupItem>();
                foreach (var d in disabled)
                {
                    if (!activeItems.Any(a => a.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase) && a.Location == d.Location))
                    {
                        result.Add(d);
                        cleanedDisabled.Add(d);
                    }
                }

                // If some disabled items are active now (enabled externally), update state file
                if (cleanedDisabled.Count != disabled.Count)
                {
                    SaveDisabledItems(cleanedDisabled);
                }

                return result;
            });
        }

        public static Task<bool> ToggleStartupItemAsync(StartupItem item, bool enable)
        {
            return Task.Run(() =>
            {
                if (enable)
                {
                    return EnableStartupItem(item);
                }
                else
                {
                    return DisableStartupItem(item);
                }
            });
        }

        private static bool DisableStartupItem(StartupItem item)
        {
            var disabled = LoadDisabledItems();

            // Prevent duplicate records
            if (disabled.Any(d => d.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && d.Location == item.Location))
            {
                return true;
            }

            bool success = false;
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
                        if (key != null)
                        {
                            key.DeleteValue(item.Name, throwOnMissingValue: false);
                            success = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to disable registry startup item: {ex.Message}");
                }
            }
            else
            {
                // File-based startup
                try
                {
                    if (File.Exists(item.Path))
                    {
                        string disabledPath = item.Path + ".disabled";
                        File.Move(item.Path, disabledPath, overwrite: true);
                        item.Path = disabledPath; // Update the path for storage
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to disable file startup item: {ex.Message}");
                }
            }

            if (success)
            {
                item.Enabled = false;
                disabled.Add(item);
                SaveDisabledItems(disabled);
                return true;
            }

            return false;
        }

        private static bool EnableStartupItem(StartupItem item)
        {
            var disabled = LoadDisabledItems();
            var found = disabled.FirstOrDefault(d => d.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && d.Location == item.Location);
            if (found == null)
            {
                return false; // Not found in disabled list
            }

            bool success = false;
            if (found.Location.Contains("Run"))
            {
                try
                {
                    var root = found.Location.StartsWith("HKCU") ? Registry.CurrentUser : Registry.LocalMachine;
                    string subkeyPath = found.Location.Contains("Wow6432Node") 
                        ? @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
                        : @"Software\Microsoft\Windows\CurrentVersion\Run";
                    using (var key = root.OpenSubKey(subkeyPath, writable: true))
                    {
                        if (key != null)
                        {
                            key.SetValue(found.Name, found.Path);
                            success = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to enable registry startup item: {ex.Message}");
                }
            }
            else
            {
                // File-based startup
                try
                {
                    if (File.Exists(found.Path))
                    {
                        string originalPath = found.Path;
                        if (originalPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            originalPath = originalPath.Substring(0, originalPath.Length - ".disabled".Length);
                        }
                        File.Move(found.Path, originalPath, overwrite: true);
                        found.Path = originalPath;
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to enable file startup item: {ex.Message}");
                }
            }

            if (success)
            {
                disabled.Remove(found);
                SaveDisabledItems(disabled);
                return true;
            }

            return false;
        }
    }
}
