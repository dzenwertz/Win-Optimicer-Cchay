using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public class BloatwareAppViewModel : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _installed;

        public BloatwareApp App { get; }

        public BloatwareAppViewModel(BloatwareApp app)
        {
            App = app;
            _isChecked = false;
            _installed = app.Installed;
        }

        public string Key => App.Key;
        public string Name => App.Name;
        public string Description => App.Description;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public bool Installed
        {
            get => _installed;
            set
            {
                _installed = value;
                App.Installed = value;
                OnPropertyChanged(nameof(Installed));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(IsCheckboxEnabled));
                OnPropertyChanged(nameof(Opacity));
            }
        }

        public string StatusText => Installed ? "Instalada" : "Desinstalada";
        public string StatusColor => Installed ? "#FA5252" : "#868E96";
        public bool IsCheckboxEnabled => Installed;
        public double Opacity => Installed ? 1.0 : 0.6;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class BloatwarePage : UserControl
    {
        private readonly ObservableCollection<BloatwareAppViewModel> _allApps = new ObservableCollection<BloatwareAppViewModel>();

        public BloatwarePage()
        {
            InitializeComponent();
            Loaded += BloatwarePage_Loaded;
        }

        private async void BloatwarePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAppsAsync();
            ListApps.ItemsSource = _allApps;
        }

        private async Task LoadAppsAsync()
        {
            _allApps.Clear();
            try
            {
                var apps = await BloatwareService.GetBloatwareListAsync();
                foreach (var app in apps)
                {
                    _allApps.Add(new BloatwareAppViewModel(app));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading apps: {ex.Message}");
            }
            UpdateBulkButtonState();
        }

        private void UpdateBulkButtonState()
        {
            int checkedCount = _allApps.Count(a => a.IsChecked);
            if (checkedCount > 0)
            {
                TxtUninstallBtn.Text = $"Desinstalar Aplicaciones Seleccionadas ({checkedCount})";
                BtnUninstallSelected.Visibility = Visibility.Visible;
            }
            else
            {
                BtnUninstallSelected.Visibility = Visibility.Collapsed;
            }

            int installedCount = _allApps.Count(a => a.Installed);
            if (installedCount > 0 && checkedCount == installedCount)
            {
                BtnToggleAll.Content = "Deseleccionar Todo";
            }
            else
            {
                BtnToggleAll.Content = "Seleccionar Todo";
            }
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateBulkButtonState();
        }

        private void BtnToggleAll_Click(object sender, RoutedEventArgs e)
        {
            var installedApps = _allApps.Where(a => a.Installed).ToList();
            if (installedApps.Count == 0) return;

            bool anyUnchecked = installedApps.Any(a => !a.IsChecked);
            foreach (var app in installedApps)
            {
                app.IsChecked = anyUnchecked;
            }
            UpdateBulkButtonState();
        }

        private async void BtnUninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedVMs = _allApps.Where(a => a.IsChecked).ToList();
            if (selectedVMs.Count == 0) return;

            SetControlsState(false);
            TxtMessage.Visibility = Visibility.Collapsed;
            TxtUninstallBtn.Text = "Desinstalando...";

            try
            {
                // Create backup first
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync("Cchay Uninstall Bloatware");
                }

                int successCount = 0;
                foreach (var vm in selectedVMs)
                {
                    bool success = await BloatwareService.UninstallAppAsync(vm.Key);
                    if (success) successCount++;
                }

                ShowMessage($"✨ Se desinstalaron con éxito {successCount} aplicaciones seleccionadas de Windows.", isError: false);
                await LoadAppsAsync();
            }
            catch (Exception ex)
            {
                ShowMessage($"⚠️ Error al desinstalar: {ex.Message}", isError: true);
            }
            finally
            {
                SetControlsState(true);
                UpdateBulkButtonState();
            }
        }

        private async void BtnOneDrive_Click(object sender, RoutedEventArgs e)
        {
            BtnOneDrive.IsEnabled = false;
            TxtMessage.Visibility = Visibility.Collapsed;
            var oldContent = BtnOneDrive.Content;
            BtnOneDrive.Content = "Desactivando...";

            try
            {
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync("Cchay Disable OneDrive");
                }
                bool success = await BloatwareService.DisableOneDriveAsync();
                if (success)
                {
                    ShowMessage("✨ Microsoft OneDrive ha sido desinstalado y deshabilitado de forma completa.", isError: false);
                }
                else
                {
                    ShowMessage("⚠️ Error al desactivar OneDrive.", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"⚠️ Excepción al desactivar OneDrive: {ex.Message}", isError: true);
            }
            finally
            {
                BtnOneDrive.IsEnabled = true;
                BtnOneDrive.Content = oldContent;
            }
        }

        private async void BtnWidgets_Click(object sender, RoutedEventArgs e)
        {
            BtnWidgets.IsEnabled = false;
            TxtMessage.Visibility = Visibility.Collapsed;
            var oldContent = BtnWidgets.Content;
            BtnWidgets.Content = "Eliminando...";

            try
            {
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync("Cchay Disable Widgets");
                }
                bool success = await BloatwareService.DisableWidgetsAsync();
                if (success)
                {
                    ShowMessage("✨ Los Widgets de Windows 11 han sido removidos con éxito.", isError: false);
                }
                else
                {
                    ShowMessage("⚠️ Error al desactivar Widgets.", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"⚠️ Excepción al desactivar Widgets: {ex.Message}", isError: true);
            }
            finally
            {
                BtnWidgets.IsEnabled = true;
                BtnWidgets.Content = oldContent;
            }
        }

        private void ShowMessage(string text, bool isError)
        {
            TxtMessage.Text = text;
            TxtMessage.Background = isError ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0xFA, 0x52, 0x52)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0x10, 0x7C, 0x41));
            TxtMessage.Foreground = isError ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0x52, 0x52)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57));
            TxtMessage.Visibility = Visibility.Visible;
        }

        private void SetControlsState(bool enabled)
        {
            BtnUninstallSelected.IsEnabled = enabled;
            BtnOneDrive.IsEnabled = enabled;
            BtnWidgets.IsEnabled = enabled;
            ListApps.IsEnabled = enabled;
        }
    }
}
