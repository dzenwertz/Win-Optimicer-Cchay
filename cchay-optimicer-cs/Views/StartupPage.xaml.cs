using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class StartupItemViewModel : INotifyPropertyChanged
    {
        private bool _enabled;
        private readonly Action<StartupItemViewModel, bool> _onToggle;
        private bool _isToggling = false;

        public StartupItem Item { get; }

        public StartupItemViewModel(StartupItem item, Action<StartupItemViewModel, bool> onToggle)
        {
            Item = item;
            _enabled = item.Enabled;
            _onToggle = onToggle;
        }

        public string Name => Item.Name;
        public string Path => Item.Path;
        public string Location => Item.Location;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value && !_isToggling)
                {
                    _enabled = value;
                    OnPropertyChanged(nameof(Enabled));
                    OnPropertyChanged(nameof(BadgeText));
                    OnPropertyChanged(nameof(BadgeColor));
                    _onToggle(this, value);
                }
            }
        }

        public string LocationLabel => Location switch
        {
            "HKCU Run" => "Registro (Usuario)",
            "HKLM Run" => "Registro (Sistema)",
            "User Startup Folder" => "Inicio (Usuario)",
            "Common Startup Folder" => "Inicio (Común)",
            _ => Location
        };

        public string BadgeText => Enabled ? "Habilitado" : "Deshabilitado";
        public string BadgeColor => Enabled ? "#40C057" : "#FA5252";

        public string CleanPath
        {
            get
            {
                string p = Item.Path;
                if (string.IsNullOrEmpty(p)) return string.Empty;
                if (p.StartsWith("\""))
                {
                    int nextQuote = p.IndexOf("\"", 1);
                    if (nextQuote > 1) return p.Substring(1, nextQuote - 1);
                }
                if (p.Contains(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    int exeIdx = p.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    return p.Substring(0, exeIdx + 4);
                }
                return p;
            }
        }

        public void SetEnabledStateWithoutTrigger(bool enabled)
        {
            _isToggling = true;
            _enabled = enabled;
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(BadgeColor));
            _isToggling = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class StartupPage : UserControl
    {
        private readonly List<StartupItemViewModel> _allStartupItems = new List<StartupItemViewModel>();
        private readonly ObservableCollection<StartupItemViewModel> _filteredStartupItems = new ObservableCollection<StartupItemViewModel>();
        private bool _isInitialized = false;

        public StartupPage()
        {
            InitializeComponent();
            Loaded += StartupPage_Loaded;
        }

        private async void StartupPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            await RefreshListAsync();
            _isInitialized = true;
        }

        private async Task RefreshListAsync()
        {
            try
            {
                var items = await StartupService.GetStartupItemsAsync();
                _allStartupItems.Clear();
                foreach (var it in items)
                {
                    _allStartupItems.Add(new StartupItemViewModel(it, OnItemToggled));
                }

                ApplyFilter();
                ListStartup.ItemsSource = _filteredStartupItems;
                UpdateStats();
            }
            catch (Exception ex)
            {
                ShowToast($"Error al cargar la lista: {ex.Message}");
            }
        }

        private void OnItemToggled(StartupItemViewModel vm, bool enabled)
        {
            _ = ToggleItemAsync(vm, enabled);
        }

        private async Task ToggleItemAsync(StartupItemViewModel vm, bool enabled)
        {
            bool success = false;
            try
            {
                // Create restore point before modifying startup entries
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync($"Cchay Startup {vm.Name}");
                }

                success = await StartupService.ToggleStartupItemAsync(vm.Item, enabled);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to toggle startup item: {ex.Message}");
            }

            if (success)
            {
                ShowToast($"¡Programa '{vm.Name}' {(enabled ? "habilitado" : "deshabilitado")}!");
            }
            else
            {
                // Revert state in UI if it failed
                vm.SetEnabledStateWithoutTrigger(!enabled);
                ShowToast($"Error al cambiar el estado del programa.");
            }

            UpdateStats();
        }

        private void ApplyFilter()
        {
            _filteredStartupItems.Clear();
            string query = TxtSearch.Text.Trim();
            var items = _allStartupItems.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(it => it.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                                          it.Path.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in items)
            {
                _filteredStartupItems.Add(item);
            }
        }

        private void UpdateStats()
        {
            int active = _allStartupItems.Count(it => it.Enabled);
            int total = _allStartupItems.Count;
            TxtActiveCount.Text = $"{active} / {total}";
        }

        private async void BtnRefresh_Click(object sender, MouseButtonEventArgs e)
        {
            await RefreshListAsync();
            ShowToast("Lista de inicio actualizada.");
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private async void ShowToast(string message)
        {
            TxtMessage.Text = message;
            TxtMessage.Visibility = Visibility.Visible;
            await Task.Delay(3500);
            if (TxtMessage.Text == message)
            {
                TxtMessage.Visibility = Visibility.Collapsed;
            }
        }
    }
}
