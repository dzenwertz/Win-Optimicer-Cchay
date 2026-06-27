using System;
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
            }
            catch (Exception ex)
            {
                TxtWelcome.Text = "Error al cargar información del sistema.";
                System.Diagnostics.Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
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
            TxtOptimizeBtn.Text = "Optimizando...";
            TxtResult.Visibility = Visibility.Collapsed;

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

                double totalFreed = ramFreedMB + diskFreedMB;
                TxtResult.Text = $"✨ ¡Optimización completada! Se liberaron {totalFreed:F0} MB de RAM y disco.";
                TxtResult.Visibility = Visibility.Visible;

                await RefreshMemoryInfoAsync();
                await RefreshDiskInfoAsync();
            }
            catch (Exception ex)
            {
                TxtResult.Text = "⚠️ Hubo un error durante la optimización.";
                TxtResult.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"Optimization error: {ex.Message}");
            }
            finally
            {
                BtnOptimize.IsEnabled = true;
                TxtOptimizeBtn.Text = "Optimizar Ahora";
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
                        "backup" => typeof(BackupPage),
                        "software" => typeof(SoftwarePage),
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
