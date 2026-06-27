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
    public class TweakViewModel : INotifyPropertyChanged
    {
        private bool _enabled;
        private bool _isToggling;

        public Tweak Tweak { get; }

        public TweakViewModel(Tweak tweak)
        {
            Tweak = tweak;
            _enabled = tweak.Enabled;
        }

        public string Key => Tweak.Key;
        public string Name => Tweak.Name;
        public string Description => Tweak.Description;
        public string Category => Tweak.Category;
        public string Risk => Tweak.Risk;

        public string RiskColor => Risk switch
        {
            "safe" => "#40C057",
            "moderate" => "#FD7E14",
            "advanced" => "#FA5252",
            _ => "#868E96"
        };

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value && !_isToggling)
                {
                    _enabled = value;
                    OnPropertyChanged(nameof(Enabled));
                    _ = ToggleAsync(value);
                }
            }
        }

        private async Task ToggleAsync(bool enable)
        {
            _isToggling = true;
            bool success = false;
            try
            {
                // Create backup restore point automatically before applying tweaks for safety (only if admin)
                if (SystemService.IsRunningAsAdmin() && SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync($"Cchay Tweak {Name}");
                }

                if (enable)
                {
                    success = await TweakService.ApplyTweakAsync(Key);
                }
                else
                {
                    success = await TweakService.UnapplyTweakAsync(Key);
                }
            }
            catch { }
            finally
            {
                if (!success)
                {
                    // Revert the toggle check status if applying fails
                    _enabled = !enable;
                    OnPropertyChanged(nameof(Enabled));

                    string errorMsg = SystemService.IsRunningAsAdmin()
                        ? "No se pudo modificar el ajuste. Hubo un error al acceder o escribir en el registro del sistema."
                        : "No se pudo modificar el ajuste debido a la falta de privilegios.\n\nPor favor, ejecuta el programa como Administrador para poder aplicar esta configuración.";

                    MessageBox.Show(
                        errorMsg,
                        "Error al Modificar Ajuste",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                _isToggling = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class TweaksPage : UserControl
    {
        private readonly List<TweakViewModel> _allTweaks = new List<TweakViewModel>();
        private readonly ObservableCollection<TweakViewModel> _filteredTweaks = new ObservableCollection<TweakViewModel>();
        private string _currentFilter = "all";

        public TweaksPage()
        {
            InitializeComponent();
            Loaded += TweaksPage_Loaded;
        }

        private async void TweaksPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_allTweaks.Count == 0)
            {
                var tweaks = await TweakService.GetTweaksAsync();
                foreach (var tw in tweaks)
                {
                    _allTweaks.Add(new TweakViewModel(tw));
                }
            }

            ApplyFilter();
            ListTweaks.ItemsSource = _filteredTweaks;
        }

        private void ApplyFilter()
        {
            _filteredTweaks.Clear();
            var query = _allTweaks.AsEnumerable();
            if (_currentFilter != "all")
            {
                query = query.Where(t => t.Category.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in query)
            {
                _filteredTweaks.Add(item);
            }
        }

        private void TabFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string filter)
            {
                _currentFilter = filter;

                TabAll.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabPerformance.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabPrivacy.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabVisual.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabGaming.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabServices.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;

                btn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;

                ApplyFilter();
            }
        }
    }
}
