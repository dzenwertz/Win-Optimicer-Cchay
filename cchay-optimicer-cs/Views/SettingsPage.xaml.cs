using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class SettingsPage : UserControl
    {
        private bool _isInitialized = false;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Settings;

            // Bind values
            ToggleMinimizeTray.IsChecked = settings.MinimizeToTray;
            ToggleAutoRestore.IsChecked = settings.AutoRestorePointEnabled;
            
            ToggleAutoRam.IsChecked = settings.AutoRamCleanEnabled;
            PanelRamThreshold.Visibility = settings.AutoRamCleanEnabled ? Visibility.Visible : Visibility.Collapsed;
            
            ToggleDailyMaintenance.IsChecked = settings.DailyMaintenanceEnabled;
            PanelDailyTime.Visibility = settings.DailyMaintenanceEnabled ? Visibility.Visible : Visibility.Collapsed;

            // Load ComboBox selections
            int currentRamThreshold = settings.AutoRamCleanThreshold;
            foreach (ComboBoxItem item in ComboRamThreshold.Items)
            {
                if (item.Tag?.ToString() == currentRamThreshold.ToString())
                {
                    ComboRamThreshold.SelectedItem = item;
                    break;
                }
            }

            string currentTime = settings.DailyMaintenanceTime;
            foreach (ComboBoxItem item in ComboDailyTime.Items)
            {
                if (item.Tag?.ToString() == currentTime)
                {
                    ComboDailyTime.SelectedItem = item;
                    break;
                }
            }

            _isInitialized = true;
        }

        private void ToggleMinimizeTray_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            SettingsService.Settings.MinimizeToTray = ToggleMinimizeTray.IsChecked == true;
            SettingsService.SaveSettings();
            ShowMessage("Preferencia de bandeja guardada.");
        }

        private void ToggleAutoRestore_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            SettingsService.Settings.AutoRestorePointEnabled = ToggleAutoRestore.IsChecked == true;
            SettingsService.SaveSettings();
            ShowMessage("Preferencia de puntos de restauración guardada.");
        }

        private void ToggleAutoRam_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            bool isEnabled = ToggleAutoRam.IsChecked == true;
            SettingsService.Settings.AutoRamCleanEnabled = isEnabled;
            PanelRamThreshold.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            SettingsService.SaveSettings();
            ShowMessage(isEnabled ? "Limpieza de RAM automática activada." : "Limpieza de RAM automática desactivada.");
        }

        private void ComboRamThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (ComboRamThreshold.SelectedItem is ComboBoxItem item && item.Tag is string thresholdStr)
            {
                if (int.TryParse(thresholdStr, out int threshold))
                {
                    SettingsService.Settings.AutoRamCleanThreshold = threshold;
                    SettingsService.SaveSettings();
                    ShowMessage($"Umbral de RAM cambiado a {threshold}%.");
                }
            }
        }

        private void ToggleDailyMaintenance_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            bool isEnabled = ToggleDailyMaintenance.IsChecked == true;
            SettingsService.Settings.DailyMaintenanceEnabled = isEnabled;
            PanelDailyTime.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            SettingsService.SaveSettings();
            ShowMessage(isEnabled ? "Mantenimiento diario activado." : "Mantenimiento diario desactivado.");
        }

        private void ComboDailyTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (ComboDailyTime.SelectedItem is ComboBoxItem item && item.Tag is string timeStr)
            {
                SettingsService.Settings.DailyMaintenanceTime = timeStr;
                SettingsService.SaveSettings();
                ShowMessage($"Hora de mantenimiento programada para las {timeStr}.");
            }
        }

        private async void ShowMessage(string message)
        {
            TxtMessage.Text = "⚙️ " + message;
            TxtMessage.Visibility = Visibility.Visible;
            await Task.Delay(2500);
            if (TxtMessage.Text == "⚙️ " + message)
            {
                TxtMessage.Visibility = Visibility.Collapsed;
            }
        }
    }
}
