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
        private bool _isInitialized = false;

        public RamPage()
        {
            InitializeComponent();
            Loaded += RamPage_Loaded;
            Unloaded += RamPage_Unloaded;
        }

        private async void RamPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshMemoryInfoAsync();

            // Load settings
            CheckAutoRam.IsChecked = SettingsService.Settings.AutoRamCleanEnabled;
            UpdateAutoRamStatusText(SettingsService.Settings.AutoRamCleanEnabled);

            // Select ComboBox item based on threshold
            int currentThreshold = SettingsService.Settings.AutoRamCleanThreshold;
            foreach (ComboBoxItem item in ComboThreshold.Items)
            {
                if (item.Tag?.ToString() == currentThreshold.ToString())
                {
                    ComboThreshold.SelectedItem = item;
                    break;
                }
            }

            _isInitialized = true;

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
            PanelRamCleanProgress.Visibility = Visibility.Visible;
            ProgRamClean.Value = 0;
            TxtRamFreedLive.Text = "0 MB liberados";

            try
            {
                long freedBytes = await RamService.CleanAllWithProgressAsync((stepText, percent, bytesSoFar) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtRamProgressStep.Text = stepText;
                        ProgRamClean.Value = percent;
                        double liveMB = bytesSoFar / 1024.0 / 1024.0;
                        TxtRamFreedLive.Text = $"{liveMB:F0} MB liberados";
                    });
                });
                double freedMB = freedBytes / 1024.0 / 1024.0;

                // Save stats
                SettingsService.Settings.TotalBytesRamFreed += (ulong)freedBytes;
                SettingsService.Settings.TotalOptimizationsRun++;
                SettingsService.SaveSettings();

                TxtFreedToast.Text = $"✨ Se liberaron aproximadamente {freedMB:F0} MB de RAM.";
                TxtFreedToast.Visibility = Visibility.Visible;
                PanelRamCleanProgress.Visibility = Visibility.Collapsed;

                await RefreshMemoryInfoAsync();
            }
            catch (Exception ex)
            {
                TxtFreedToast.Text = "Hubo un error al optimizar la memoria.";
                TxtFreedToast.Visibility = Visibility.Visible;
                PanelRamCleanProgress.Visibility = Visibility.Collapsed;
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
                    Wpf.Ui.Controls.ProgressRing? ring = null;

                    switch (areaKey)
                    {
                        case "workingSet":
                            ring = Ring_workingSet;
                            if (ring != null) ring.Visibility = Visibility.Visible;
                            freedBytes = await RamService.CleanWorkingSet();
                            statusLabel = TxtFreed_workingSet;
                            break;
                        case "standby":
                            ring = Ring_standby;
                            if (ring != null) ring.Visibility = Visibility.Visible;
                            freedBytes = await RamService.CleanStandbyList();
                            statusLabel = TxtFreed_standby;
                            break;
                        case "fileCache":
                            ring = Ring_fileCache;
                            if (ring != null) ring.Visibility = Visibility.Visible;
                            freedBytes = await RamService.CleanSystemFileCache();
                            statusLabel = TxtFreed_fileCache;
                            break;
                        case "modified":
                            ring = Ring_modified;
                            if (ring != null) ring.Visibility = Visibility.Visible;
                            freedBytes = await RamService.CleanModifiedPageList();
                            statusLabel = TxtFreed_modified;
                            break;
                        case "combined":
                            ring = Ring_combined;
                            if (ring != null) ring.Visibility = Visibility.Visible;
                            freedBytes = await RamService.CleanCombinedPageList();
                            statusLabel = TxtFreed_combined;
                            break;
                    }

                    if (ring != null) ring.Visibility = Visibility.Collapsed;

                    // Save stats
                    if (freedBytes > 0)
                    {
                        SettingsService.Settings.TotalBytesRamFreed += (ulong)freedBytes;
                        SettingsService.SaveSettings();
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
                    
                    // Ensure rings are hidden on error
                    if (areaKey == "workingSet") Ring_workingSet.Visibility = Visibility.Collapsed;
                    if (areaKey == "standby") Ring_standby.Visibility = Visibility.Collapsed;
                    if (areaKey == "fileCache") Ring_fileCache.Visibility = Visibility.Collapsed;
                    if (areaKey == "modified") Ring_modified.Visibility = Visibility.Collapsed;
                    if (areaKey == "combined") Ring_combined.Visibility = Visibility.Collapsed;
                }
                finally
                {
                    btn.IsEnabled = true;
                    btn.Content = oldContent;
                }
            }
        }

        private void CheckAutoRam_Click(object sender, RoutedEventArgs e)
        {
            if (CheckAutoRam == null) return;
            bool isChecked = CheckAutoRam.IsChecked == true;
            SettingsService.Settings.AutoRamCleanEnabled = isChecked;
            SettingsService.SaveSettings();
            UpdateAutoRamStatusText(isChecked);
        }

        private void ComboThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || ComboThreshold == null) return;
            if (ComboThreshold.SelectedItem is ComboBoxItem item && item.Tag is string thresholdStr)
            {
                if (int.TryParse(thresholdStr, out int threshold))
                {
                    SettingsService.Settings.AutoRamCleanThreshold = threshold;
                    SettingsService.SaveSettings();
                }
            }
        }

        private void UpdateAutoRamStatusText(bool enabled)
        {
            if (TxtAutoRamStatus == null) return;
            TxtAutoRamStatus.Text = enabled ? "Activo" : "Desactivado";
            TxtAutoRamStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                enabled ? System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57) : System.Windows.Media.Color.FromRgb(0x86, 0x8E, 0x96));
        }
    }
}
