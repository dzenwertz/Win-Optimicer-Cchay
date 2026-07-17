using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class ServicesPage : UserControl
    {
        private List<ServiceViewModel> _allServices = new List<ServiceViewModel>();
        private bool _isLoaded = false;
        private bool _isApplyingProfile = false;

        public ServicesPage()
        {
            InitializeComponent();
            Loaded += ServicesPage_Loaded;
        }

        private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            await LoadServicesAsync();
            _isLoaded = true;
        }

        private async Task LoadServicesAsync()
        {
            BtnOptimizeAll.IsEnabled = false;
            var services = await WindowsServicesService.GetServicesAsync();
            
            _allServices.Clear();
            foreach (var s in services)
            {
                _allServices.Add(new ServiceViewModel(s));
            }

            ApplyFilters();
            await UpdateProfileCountsAsync();
            BtnOptimizeAll.IsEnabled = true;
        }

        private async Task UpdateProfileCountsAsync()
        {
            try
            {
                var rawModels = _allServices.Select(s => s.Model).ToList();
                
                var recCount = (await WindowsServicesService.GetServicesForProfileAsync("recommended", rawModels)).Count;
                var gamCount = (await WindowsServicesService.GetServicesForProfileAsync("gaming", rawModels)).Count;
                var maxCount = (await WindowsServicesService.GetServicesForProfileAsync("maximum", rawModels)).Count;

                TxtRecommendedCount.Text = recCount > 0 ? $"{recCount} servicios por optimizar" : "✓ Ya optimizado";
                TxtGamingCount.Text = gamCount > 0 ? $"{gamCount} servicios por optimizar" : "✓ Ya optimizado";
                TxtMaximumCount.Text = maxCount > 0 ? $"{maxCount} servicios por optimizar" : "✓ Ya optimizado";
            }
            catch { }
        }

        private void ApplyFilters()
        {
            string query = TxtSearch.Text.Trim().ToLower();
            var filtered = _allServices.AsEnumerable();

            // Text search
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(s => s.Name.ToLower().Contains(query) || 
                                               s.DisplayName.ToLower().Contains(query) || 
                                               s.Description.ToLower().Contains(query));
            }

            // Radio Button filters
            if (RadRunning.IsChecked == true)
            {
                filtered = filtered.Where(s => s.Status == "En ejecución");
            }
            else if (RadStopped.IsChecked == true)
            {
                filtered = filtered.Where(s => s.Status == "Detenido");
            }
            else if (RadDisabled.IsChecked == true)
            {
                filtered = filtered.Where(s => s.StartupType == "Deshabilitado");
            }
            else if (RadBloat.IsChecked == true)
            {
                filtered = filtered.Where(s => s.RiskLevel == "bloat");
            }

            ListServices.ItemsSource = filtered.OrderBy(s => s.DisplayName).ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
            {
                ApplyFilters();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  PROFILE APPLICATION
        // ═══════════════════════════════════════════════════════════════

        private async void Profile_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isApplyingProfile) return;
            if (sender is not FrameworkElement element || element.Tag is not string profileKey) return;

            var profiles = WindowsServicesService.GetProfiles();
            var profile = profiles.FirstOrDefault(p => p.Key == profileKey);
            if (profile == null) return;

            // Check how many services will be affected
            var rawModels = _allServices.Select(s => s.Model).ToList();
            var toDisable = await WindowsServicesService.GetServicesForProfileAsync(profileKey, rawModels);

            if (toDisable.Count == 0)
            {
                TxtResult.Text = $"✅ ¡El perfil {profile.Name} ya está completamente aplicado! No hay servicios adicionales que optimizar.";
                TxtResult.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57));
                TxtResult.Visibility = Visibility.Visible;
                return;
            }

            // Confirmation dialog
            string warningText = profileKey switch
            {
                "recommended" => "Este perfil es seguro y no afectará la funcionalidad normal de tu PC.",
                "gaming" => "⚠️ Este perfil desactiva Xbox, Bluetooth, búsqueda de Windows y Superfetch. No lo uses si necesitas esos servicios.",
                "maximum" => "⚠️ ¡ADVERTENCIA! Este perfil es agresivo. Desactiva escritorio remoto, geolocalización, biometría, UPnP y más. Solo para usuarios avanzados.",
                _ => ""
            };

            var result = MessageBox.Show(
                $"Perfil: {profile.Name}\n\n" +
                $"Se deshabilitarán y detendrán {toDisable.Count} servicios de Windows.\n\n" +
                $"{warningText}\n\n" +
                "¿Deseas continuar?",
                "Aplicar Perfil de Optimización",
                MessageBoxButton.YesNo,
                profileKey == "maximum" ? MessageBoxImage.Warning : MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Create restore point first
            if (SettingsService.Settings.AutoRestorePointEnabled)
            {
                try
                {
                    TxtResult.Text = "🔄 Creando punto de restauración antes de aplicar el perfil...";
                    TxtResult.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x86, 0x8E, 0x96));
                    TxtResult.Visibility = Visibility.Visible;
                    await BackupService.CreateRestorePointAsync($"Cchay Services Profile: {profile.Name}");
                }
                catch { }
            }

            // Apply profile with progress
            _isApplyingProfile = true;
            BtnOptimizeAll.IsEnabled = false;
            PanelProfileProgress.Visibility = Visibility.Visible;
            ProgProfile.Value = 0;
            TxtResult.Visibility = Visibility.Collapsed;

            try
            {
                int applied = await WindowsServicesService.ApplyProfileAsync(profileKey, rawModels, (serviceName, done, total) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        double percent = total > 0 ? (double)done / total * 100 : 0;
                        ProgProfile.Value = percent;
                        TxtProfileProgress.Text = $"Deshabilitando {serviceName}... ({done}/{total})";
                    });
                });

                // Refresh the service list to reflect changes
                await LoadServicesAsync();

                MainWindow.Instance?.ShowRebootRequired();

                TxtResult.Text = $"✅ ¡Perfil {profile.Name} aplicado con éxito! Se optimizaron {applied} servicios.";
                TxtResult.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57));
                TxtResult.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtResult.Text = $"⚠️ Error al aplicar el perfil: {ex.Message}";
                TxtResult.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0x52, 0x52));
                TxtResult.Visibility = Visibility.Visible;
            }
            finally
            {
                _isApplyingProfile = false;
                PanelProfileProgress.Visibility = Visibility.Collapsed;
                BtnOptimizeAll.IsEnabled = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  INDIVIDUAL SERVICE ACTIONS
        // ═══════════════════════════════════════════════════════════════

        private async void BtnServiceAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ServiceViewModel vm)
            {
                btn.IsEnabled = false;
                string newAction = vm.Status == "En ejecución" ? "stop" : "start";
                bool success = await WindowsServicesService.ChangeServiceStatusAsync(vm.Name, newAction);
                if (success)
                {
                    vm.Status = newAction == "stop" ? "Detenido" : "En ejecución";
                }
                btn.IsEnabled = true;
            }
        }

        private async void StartupType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is ComboBox comboBox && comboBox.DataContext is ServiceViewModel vm)
            {
                if (comboBox.SelectedValue is string newType)
                {
                    if (vm.StartupType != newType)
                    {
                        comboBox.IsEnabled = false;
                        bool success = await WindowsServicesService.ChangeServiceStartupTypeAsync(vm.Name, newType);
                        if (success)
                        {
                            vm.StartupType = newType;
                            MainWindow.Instance?.ShowRebootRequired();
                        }
                        comboBox.IsEnabled = true;
                        await UpdateProfileCountsAsync();
                    }
                }
            }
        }

        private async void BtnOptimizeAll_Click(object sender, RoutedEventArgs e)
        {
            var bloatServices = _allServices.Where(s => s.RiskLevel == "bloat" && s.StartupType != "Deshabilitado").ToList();
            if (bloatServices.Count == 0)
            {
                MessageBox.Show("Todos los servicios optimizables ya están deshabilitados.", "Servicios de Windows", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Se deshabilitarán y detendrán {bloatServices.Count} servicios innecesarios detectados (Xbox, Telemetría, Mapas, etc.). ¿Deseas continuar?", "Optimizar Servicios", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                BtnOptimizeAll.IsEnabled = false;
                foreach (var s in bloatServices)
                {
                    await WindowsServicesService.ChangeServiceStartupTypeAsync(s.Name, "Deshabilitado");
                    await WindowsServicesService.ChangeServiceStatusAsync(s.Name, "stop");
                    s.StartupType = "Deshabilitado";
                    s.Status = "Detenido";
                }
                BtnOptimizeAll.IsEnabled = true;
                MessageBox.Show("Se optimizaron todos los servicios seleccionados correctamente.", "Optimización Finalizada", MessageBoxButton.OK, MessageBoxImage.Information);
                ApplyFilters();
                await UpdateProfileCountsAsync();
                MainWindow.Instance?.ShowRebootRequired();
            }
        }
    }

    public class ServiceViewModel : INotifyPropertyChanged
    {
        public WindowsServiceInfo Model { get; }

        public ServiceViewModel(WindowsServiceInfo model)
        {
            Model = model;
        }

        public string Name => Model.Name;
        public string DisplayName => Model.DisplayName;
        public string Description => Model.Description;
        public string RiskLevel => Model.RiskLevel;

        public string Status
        {
            get => Model.Status;
            set
            {
                Model.Status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusBadgeBg));
                OnPropertyChanged(nameof(StatusBadgeFg));
                OnPropertyChanged(nameof(ActionButtonText));
            }
        }

        public string StartupType
        {
            get => Model.StartupType;
            set
            {
                Model.StartupType = value;
                OnPropertyChanged(nameof(StartupType));
                OnPropertyChanged(nameof(StartupTypeBadgeBg));
                OnPropertyChanged(nameof(StartupTypeBadgeFg));
            }
        }

        public bool IsConfigurable => Model.RiskLevel != "critical";

        public string ActionButtonText => Status == "En ejecución" ? "Detener" : "Iniciar";

        public string StatusBadgeBg => Status == "En ejecución" ? "#1A107C41" : "#1A6F7B8A";
        public string StatusBadgeFg => Status == "En ejecución" ? "#40C057" : "#868E96";

        public string StartupTypeBadgeBg => StartupType switch
        {
            "Automático" => "#1A007ACC",
            "Manual" => "#1A6F7B8A",
            "Deshabilitado" => "#1AF03E3E",
            _ => "#1A6F7B8A"
        };

        public string StartupTypeBadgeFg => StartupType switch
        {
            "Automático" => "#007ACC",
            "Manual" => "#868E96",
            "Deshabilitado" => "#FA5252",
            _ => "#868E96"
        };

        public string RiskBadgeBg => RiskLevel switch
        {
            "critical" => "#1AF03E3E",
            "bloat" => "#1A40C057",
            _ => "#1A6F7B8A"
        };

        public string RiskBadgeFg => RiskLevel switch
        {
            "critical" => "#FA5252",
            "bloat" => "#40C057",
            _ => "#868E96"
        };

        public string RiskBadgeText => RiskLevel switch
        {
            "critical" => "CRÍTICO (Intocable)",
            "bloat" => "OPTIMIZABLE",
            _ => "NORMAL"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
