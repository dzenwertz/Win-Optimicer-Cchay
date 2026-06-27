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
    public class DiskTargetViewModel : INotifyPropertyChanged
    {
        private double _sizeMB = -1;
        private bool _cleaned = false;
        private bool _isChecked = true;
        private int _itemsCount = 0;

        public CleanTarget Target { get; }

        public DiskTargetViewModel(CleanTarget target)
        {
            Target = target;
            _isChecked = target.IsChecked;
        }

        public string Key => Target.Key;
        public string Name => Target.Name;
        public string Description => Target.Description;
        public string Category => Target.Category;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                Target.IsChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public double SizeMB
        {
            get => _sizeMB;
            set
            {
                _sizeMB = value;
                Target.SizeMB = value;
                OnPropertyChanged(nameof(SizeMB));
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(SizeVisibility));
            }
        }

        public bool Cleaned
        {
            get => _cleaned;
            set
            {
                _cleaned = value;
                Target.Cleaned = value;
                OnPropertyChanged(nameof(Cleaned));
                OnPropertyChanged(nameof(CleanedVisibility));
            }
        }

        public int ItemsCount
        {
            get => _itemsCount;
            set
            {
                _itemsCount = value;
                Target.ItemsCount = value;
                OnPropertyChanged(nameof(ItemsCount));
            }
        }

        public string SizeText => SizeMB >= 0 
            ? (SizeMB == 0 ? "0 MB" : (SizeMB < 1 ? $"{(SizeMB * 1024):F0} KB" : $"{SizeMB:F1} MB")) 
            : "";

        public Visibility SizeVisibility => SizeMB > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CleanedVisibility => Cleaned ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class DiskPage : UserControl
    {
        private readonly List<DiskTargetViewModel> _allTargets = new List<DiskTargetViewModel>();
        private readonly ObservableCollection<DiskTargetViewModel> _filteredTargets = new ObservableCollection<DiskTargetViewModel>();
        private string _currentFilter = "all";

        public DiskPage()
        {
            InitializeComponent();
            Loaded += DiskPage_Loaded;
        }

        private void DiskPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_allTargets.Count == 0)
            {
                var targets = DiskService.GetTargets();
                foreach (var t in targets)
                {
                    var vm = new DiskTargetViewModel(t);
                    _allTargets.Add(vm);
                }
            }

            ApplyFilter();
            ListTargets.ItemsSource = _filteredTargets;
            UpdateSelectedStats();
        }

        private void ApplyFilter()
        {
            _filteredTargets.Clear();
            var query = _allTargets.AsEnumerable();
            if (_currentFilter != "all")
            {
                query = query.Where(t => t.Category.Equals(_currentFilter, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in query)
            {
                _filteredTargets.Add(item);
            }
        }

        private void UpdateSelectedStats()
        {
            int selectedCount = _allTargets.Count(t => t.IsChecked);
            TxtSelectedCount.Text = $"{selectedCount} / {_allTargets.Count}";

            if (_allTargets.Count == 0) return;

            if (selectedCount == _allTargets.Count)
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
            UpdateSelectedStats();
        }

        private void BtnToggleAll_Click(object sender, RoutedEventArgs e)
        {
            bool anyUnchecked = _allTargets.Any(t => !t.IsChecked);
            foreach (var t in _allTargets)
            {
                t.IsChecked = anyUnchecked;
            }
            UpdateSelectedStats();
        }

        private void TabFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string filter)
            {
                _currentFilter = filter;
                
                // Highlight active button
                TabAll.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabSystem.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabBrowser.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                TabWindows.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;

                btn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;

                ApplyFilter();
            }
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allTargets.Where(t => t.IsChecked).ToList();
            if (selectedItems.Count == 0) return;

            SetButtonsState(false);
            TxtBtnPreview.Text = "Analizando...";
            CardFreed.Visibility = Visibility.Collapsed;

            try
            {
                var tasks = selectedItems.Select(async vm =>
                {
                    var processed = await DiskService.ProcessTargetAsync(vm.Target, delete: false);
                    vm.SizeMB = processed.SizeMB;
                    vm.ItemsCount = processed.ItemsCount;
                    vm.Cleaned = false;
                    return processed.SizeMB > 0 ? processed.SizeMB : 0;
                });

                var sizes = await Task.WhenAll(tasks);
                double totalMB = sizes.Sum();

                TxtPreviewSize.Text = totalMB == 0 ? "0 MB" : (totalMB < 1 ? $"{(totalMB * 1024):F0} KB" : $"{totalMB:F1} MB");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk preview error: {ex.Message}");
            }
            finally
            {
                SetButtonsState(true);
                TxtBtnPreview.Text = "Previsualizar Espacio";
            }
        }

        private async void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allTargets.Where(t => t.IsChecked).ToList();
            if (selectedItems.Count == 0) return;

            SetButtonsState(false);
            TxtBtnClean.Text = "Limpiando...";
            CardFreed.Visibility = Visibility.Collapsed;

            try
            {
                // Create backup first
                if (SettingsService.Settings.AutoRestorePointEnabled)
                {
                    await BackupService.CreateRestorePointAsync("Cchay Disk Cleanup");
                }

                var tasks = selectedItems.Select(async vm =>
                {
                    var processed = await DiskService.ProcessTargetAsync(vm.Target, delete: true);
                    vm.SizeMB = -1; // Clear size preview
                    vm.Cleaned = true;
                    return processed.SizeMB > 0 ? processed.SizeMB : 0;
                });

                var sizes = await Task.WhenAll(tasks);
                double totalFreedMB = sizes.Sum();

                TxtFreedSize.Text = totalFreedMB == 0 ? "0 MB" : (totalFreedMB < 1 ? $"{(totalFreedMB * 1024):F0} KB" : $"{totalFreedMB:F1} MB");
                CardFreed.Visibility = Visibility.Visible;
                TxtPreviewSize.Text = "0 MB";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk cleanup error: {ex.Message}");
            }
            finally
            {
                SetButtonsState(true);
                TxtBtnClean.Text = "Iniciar Limpieza";
            }
        }

        private void SetButtonsState(bool enabled)
        {
            BtnPreview.IsEnabled = enabled;
            BtnClean.IsEnabled = enabled;
        }
    }
}
