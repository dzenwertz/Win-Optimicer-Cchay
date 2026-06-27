using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class RamPage : UserControl
    {
        private DispatcherTimer? _timer;

        public RamPage()
        {
            InitializeComponent();
            Loaded += RamPage_Loaded;
            Unloaded += RamPage_Unloaded;
        }

        private async void RamPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshMemoryInfoAsync();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void RamPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await RefreshMemoryInfoAsync();
        }

        private async Task RefreshMemoryInfoAsync()
        {
            await Task.Run(() =>
            {
                var mem = RamService.GetMemoryUsage();
                Dispatcher.Invoke(() =>
                {
                    double totalGB = mem.TotalBytes / 1024.0 / 1024.0 / 1024.0;
                    double usedGB = mem.UsedBytes / 1024.0 / 1024.0 / 1024.0;
                    double freeGB = mem.FreeBytes / 1024.0 / 1024.0 / 1024.0;

                    TxtRamPercent.Text = $"{mem.PercentUsed}%";
                    ProgRam.Value = mem.PercentUsed;
                    TxtRamUsed.Text = $"Usado: {usedGB:F1} GB";
                    TxtRamFree.Text = $"Libre: {freeGB:F1} GB";
                    TxtRamTotal.Text = $"Total: {totalGB:F1} GB";
                });
            });
        }

        private async void BtnCleanAll_Click(object sender, RoutedEventArgs e)
        {
            BtnCleanAll.IsEnabled = false;
            TxtCleanAll.Text = "Limpiando RAM...";
            TxtFreedToast.Visibility = Visibility.Collapsed;

            try
            {
                long freedBytes = await RamService.CleanAll();
                double freedMB = freedBytes / 1024.0 / 1024.0;

                TxtFreedToast.Text = $"✨ Se liberaron aproximadamente {freedMB:F0} MB de RAM.";
                TxtFreedToast.Visibility = Visibility.Visible;

                await RefreshMemoryInfoAsync();
            }
            catch (Exception ex)
            {
                TxtFreedToast.Text = "⚠️ Hubo un error al optimizar la memoria.";
                TxtFreedToast.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"Error cleaning RAM: {ex.Message}");
            }
            finally
            {
                BtnCleanAll.IsEnabled = true;
                TxtCleanAll.Text = "Limpiar Toda la RAM (5 áreas)";
            }
        }

        private async void BtnCleanArea_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string areaKey)
            {
                btn.IsEnabled = false;
                var oldContent = btn.Content;
                btn.Content = "...";
                TxtFreedToast.Visibility = Visibility.Collapsed;

                try
                {
                    long freedBytes = 0;
                    TextBlock? statusLabel = null;

                    switch (areaKey)
                    {
                        case "workingSet":
                            freedBytes = await RamService.CleanWorkingSet();
                            statusLabel = TxtFreed_workingSet;
                            break;
                        case "standby":
                            freedBytes = await RamService.CleanStandbyList();
                            statusLabel = TxtFreed_standby;
                            break;
                        case "fileCache":
                            freedBytes = await RamService.CleanSystemFileCache();
                            statusLabel = TxtFreed_fileCache;
                            break;
                        case "modified":
                            freedBytes = await RamService.CleanModifiedPageList();
                            statusLabel = TxtFreed_modified;
                            break;
                        case "combined":
                            freedBytes = await RamService.CleanCombinedPageList();
                            statusLabel = TxtFreed_combined;
                            break;
                    }

                    double freedMB = freedBytes / 1024.0 / 1024.0;

                    if (statusLabel != null)
                    {
                        statusLabel.Text = $"✓ {freedMB:F0} MB liberados";
                        statusLabel.Visibility = Visibility.Visible;
                    }

                    await RefreshMemoryInfoAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cleaning area {areaKey}: {ex.Message}");
                }
                finally
                {
                    btn.IsEnabled = true;
                    btn.Content = oldContent;
                }
            }
        }
    }
}
