using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class DashboardPage : UserControl
    {
        private DispatcherTimer? _timer;
        private SystemInfoData? _systemInfo;
        private DateTime _lastCpuTime = DateTime.UtcNow;
        private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSystemDataAsync();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await RefreshMemoryInfoAsync();
            await RefreshDiskInfoAsync();
            UpdateAppResourceUsage();
        }

        private async Task LoadSystemDataAsync()
        {
            try
            {
                _systemInfo = await SystemService.GetSystemInfoAsync();

                TxtWelcome.Text = $"Bienvenido, {_systemInfo.Username}. Aquí tienes el estado de tu sistema.";
                TxtCpuModel.Text = _systemInfo.CpuModel;
                TxtCpuCores.Text = $"{_systemInfo.CpuCores} cores / {_systemInfo.CpuThreads} threads";
                TxtGpuModel.Text = _systemInfo.GpuModel;
                TxtOsVersion.Text = $"{_systemInfo.OsName} {_systemInfo.OsVersion} (Build {_systemInfo.OsBuild})";

                UpdateMemoryUI(_systemInfo.MemoryTotal, _systemInfo.MemoryUsed, _systemInfo.MemoryFree, _systemInfo.MemoryPercent);
                UpdateDiskUI(_systemInfo.DiskTotal, _systemInfo.DiskUsed, _systemInfo.DiskFree, _systemInfo.DiskPercent);

                // Load Diagnostics Info
                TxtCpuTemp.Text = _systemInfo.CpuTemperature;
                TxtSmart.Text = _systemInfo.SmartStatus;
                TxtUptime.Text = _systemInfo.Uptime;
                TxtLocalIp.Text = _systemInfo.LocalIp;
                TxtPublicIp.Text = _systemInfo.PublicIp;
                TxtAdminStatus.Text = _systemInfo.IsAdmin ? "SÍ" : "NO";
                TxtAdminStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    _systemInfo.IsAdmin ? System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57) : System.Windows.Media.Color.FromRgb(0xFA, 0x52, 0x52));

                // Health Score UI update
                UpdateHealthScoreUI(_systemInfo.HealthScore);

                // Load cumulative stats
                LoadStatistics();

                // Show app self consumption
                UpdateAppResourceUsage();
            }
            catch (Exception ex)
            {
                TxtWelcome.Text = "Error al cargar información del sistema.";
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
            }
        }

        private void UpdateHealthScoreUI(int score)
        {
            TxtHealthScore.Text = score.ToString();
            
            string status;
            System.Windows.Media.Color color;

            if (score >= 90)
            {
                status = "Excelente";
                color = System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57);
            }
            else if (score >= 70)
            {
                status = "Bueno";
                color = System.Windows.Media.Color.FromRgb(0xFD, 0x7E, 0x14);
            }
            else
            {
                status = "Crítico";
                color = System.Windows.Media.Color.FromRgb(0xFA, 0x52, 0x52);
            }

            TxtHealthStatus.Text = status;
            TxtHealthStatus.Foreground = new System.Windows.Media.SolidColorBrush(color);
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_systemInfo == null) return;

            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Archivo de texto (*.txt)|*.txt",
                    FileName = $"Reporte_Sistema_{Environment.MachineName}.txt"
                };

                if (sfd.ShowDialog() == true)
                {
                    string report = SystemService.GenerateSystemReport(_systemInfo);
                    File.WriteAllText(sfd.FileName, report);
                    MessageBox.Show("Reporte de sistema exportado con éxito.", "Exportar Reporte", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar reporte: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshMemoryInfoAsync()
        {
            await Task.Run(() =>
            {
                var mem = RamService.GetMemoryUsage();
                Dispatcher.Invoke(() =>
                {
                    UpdateMemoryUI(mem.TotalBytes, mem.UsedBytes, mem.FreeBytes, mem.PercentUsed);
                });
            });
        }

        private async Task RefreshDiskInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var drive = new DriveInfo("C");
                    if (drive.IsReady)
                    {
                        long total = drive.TotalSize;
                        long free = drive.AvailableFreeSpace;
                        long used = total - free;
                        int percent = total > 0 ? (int)((double)used / total * 100) : 0;

                        Dispatcher.Invoke(() =>
                        {
                            UpdateDiskUI(total, used, free, percent);
                        });
                    }
                }
                catch { }
            });
        }

        private void UpdateMemoryUI(ulong total, ulong used, ulong free, int percent)
        {
            double totalGB = total / 1024.0 / 1024.0 / 1024.0;
            double usedGB = used / 1024.0 / 1024.0 / 1024.0;
            double freeGB = free / 1024.0 / 1024.0 / 1024.0;

            TxtRamValue.Text = $"{usedGB:F1} GB / {totalGB:F1} GB";
            ProgRam.Value = percent;
            TxtRamPercent.Text = $"{percent}% usado — {freeGB:F1} GB libres";
        }

        private void UpdateDiskUI(long total, long used, long free, int percent)
        {
            double totalGB = total / 1024.0 / 1024.0 / 1024.0;
            double usedGB = used / 1024.0 / 1024.0 / 1024.0;
            double freeGB = free / 1024.0 / 1024.0 / 1024.0;

            TxtDiskValue.Text = $"{usedGB:F1} GB / {totalGB:F1} GB";
            ProgDisk.Value = percent;
            TxtDiskPercent.Text = $"{percent}% usado — {freeGB:F1} GB libres";
        }

        private async void BtnOptimize_Click(object sender, RoutedEventArgs e)
        {
            BtnOptimize.IsEnabled = false;
            BtnTechOptimize.IsEnabled = false;
            TxtOptimizeBtn.Text = "Limpiando...";
            TxtResult.Visibility = Visibility.Collapsed;
            PanelTechProgress.Visibility = Visibility.Collapsed;
            _timer?.Stop();

            try
            {
                // Create backup first
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync("Cchay Quick Optimize");
                }

                // Clean RAM
                long ramFreed = await RamService.CleanAll();
                double ramFreedMB = ramFreed / 1024.0 / 1024.0;

                // Clean select temp disk files
                double diskFreedMB = 0;
                var targets = DiskService.GetTargets().Where(t => t.Key == "temp" || t.Key == "windowsTemp" || t.Key == "recycleBin" || t.Key == "thumbnails");
                foreach (var t in targets)
                {
                    var processed = await DiskService.ProcessTargetAsync(t, delete: true);
                    diskFreedMB += processed.SizeMB;
                }

                // Update cumulative stats
                SettingsService.Settings.TotalBytesRamFreed += (ulong)ramFreed;
                SettingsService.Settings.TotalMbDiskFreed += diskFreedMB;
                SettingsService.Settings.TotalOptimizationsRun++;
                SettingsService.SaveSettings();

                double totalFreed = ramFreedMB + diskFreedMB;
                TxtResult.Text = $"✨ ¡Limpieza rápida completada! Se liberaron {totalFreed:F0} MB de RAM y disco.";
                TxtResult.Visibility = Visibility.Visible;

                await RefreshMemoryInfoAsync();
                await RefreshDiskInfoAsync();
                await LoadSystemDataAsync();
            }
            catch (Exception ex)
            {
                TxtResult.Text = "⚠️ Hubo un error durante la limpieza rápida.";
                TxtResult.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"Optimization error: {ex.Message}");
            }
            finally
            {
                BtnOptimize.IsEnabled = true;
                BtnTechOptimize.IsEnabled = true;
                TxtOptimizeBtn.Text = "Limpieza Rápida";
                _timer?.Start();
            }
        }

        private async void BtnTechOptimize_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show("Estás por iniciar el Modo Técnico de Cchay Optimicer. Se aplicarán tweaks de registro, se deshabilitará bloatware, se optimizará la red y se liberará espacio. ¿Deseas continuar?", "Modo Técnico 🔥", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            BtnOptimize.IsEnabled = false;
            BtnTechOptimize.IsEnabled = false;
            TxtResult.Visibility = Visibility.Collapsed;
            PanelTechProgress.Visibility = Visibility.Visible;
            ProgTechOptimize.Value = 0;
            _timer?.Stop();

            try
            {
                await QuickOptimizeService.RunQuickOptimizeAsync((statusText, progressPercent) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtTechProgress.Text = statusText;
                        ProgTechOptimize.Value = progressPercent;
                    });
                });

                // Update stats
                SettingsService.Settings.TotalOptimizationsRun++;
                SettingsService.SaveSettings();

                MainWindow.Instance?.ShowRebootRequired();

                TxtResult.Text = "🔥 ¡Optimización técnica del sistema completada con éxito!";
                TxtResult.Visibility = Visibility.Visible;
                PanelTechProgress.Visibility = Visibility.Collapsed;

                await RefreshMemoryInfoAsync();
                await RefreshDiskInfoAsync();
                await LoadSystemDataAsync();
            }
            catch (Exception ex)
            {
                TxtResult.Text = "⚠️ Hubo un error durante la optimización técnica.";
                TxtResult.Visibility = Visibility.Visible;
                PanelTechProgress.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"Tech optimization error: {ex.Message}");
            }
            finally
            {
                BtnOptimize.IsEnabled = true;
                BtnTechOptimize.IsEnabled = true;
                TxtTechOptimizeBtn.Text = "Modo Técnico 🔥";
                _timer?.Start();
            }
        }

        private void LoadStatistics()
        {
            var settings = SettingsService.Settings;

            // RAM stats
            double ramGB = settings.TotalBytesRamFreed / 1024.0 / 1024.0 / 1024.0;
            TxtTotalRamFreed.Text = ramGB >= 1.0 ? $"{ramGB:F1} GB" : $"{(settings.TotalBytesRamFreed / 1024.0 / 1024.0):F0} MB";

            // Disk stats
            double diskGB = settings.TotalMbDiskFreed / 1024.0;
            TxtTotalDiskFreed.Text = diskGB >= 1.0 ? $"{diskGB:F1} GB" : $"{settings.TotalMbDiskFreed:F0} MB";

            // Total optimizations
            TxtTotalOptimizations.Text = settings.TotalOptimizationsRun.ToString();
        }

        private void UpdateAppResourceUsage()
        {
            try
            {
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    currentProcess.Refresh();

                    // RAM usage of current process
                    long ramBytes = currentProcess.WorkingSet64;
                    double ramMB = ramBytes / 1024.0 / 1024.0;
                    TxtAppRam.Text = $"{ramMB:F1} MB";

                    // CPU usage of current process
                    var now = DateTime.UtcNow;
                    var cpuTime = currentProcess.TotalProcessorTime;
                    
                    if (_lastTotalProcessorTime != TimeSpan.Zero)
                    {
                        double systemTimePassed = (now - _lastCpuTime).TotalMilliseconds * Environment.ProcessorCount;
                        double appTimePassed = (cpuTime - _lastTotalProcessorTime).TotalMilliseconds;
                        
                        double cpuPercent = systemTimePassed > 0 ? (appTimePassed / systemTimePassed) * 100.0 : 0.0;
                        TxtAppCpu.Text = $"{Math.Min(100.0, Math.Max(0.0, cpuPercent)):F2}%";
                    }
                    else
                    {
                        TxtAppCpu.Text = "0.00%";
                    }

                    _lastCpuTime = now;
                    _lastTotalProcessorTime = cpuTime;
                }
            }
            catch
            {
                TxtAppRam.Text = "N/A";
                TxtAppCpu.Text = "N/A";
            }
        }

        private void NavigateCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string pageKey)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    Type? pageType = pageKey switch
                    {
                        "dashboard" => typeof(DashboardPage),
                        "ram" => typeof(RamPage),
                        "disk" => typeof(DiskPage),
                        "tweaks" => typeof(TweaksPage),
                        "bloatware" => typeof(BloatwarePage),
                        "network" => typeof(NetworkPage),
                        "startup" => typeof(StartupPage),
                        "services" => typeof(ServicesPage),
                        "backup" => typeof(BackupPage),
                        "software" => typeof(SoftwarePage),
                        "scanner" => typeof(ScannerPage),
                        "repair" => typeof(RepairPage),
                        "settings" => typeof(SettingsPage),
                        _ => null
                    };

                    if (pageType != null)
                    {
                        mainWindow.Navigate(pageType);
                    }
                }
            }
        }
    }
}
