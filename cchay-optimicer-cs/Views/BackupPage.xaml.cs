using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class BackupPage : UserControl
    {
        private readonly ObservableCollection<RestorePoint> _points = new ObservableCollection<RestorePoint>();
        private bool _isConfirmingDelete = false;

        public BackupPage()
        {
            InitializeComponent();
            Loaded += BackupPage_Loaded;
        }

        private async void BackupPage_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleAutoRestore.IsChecked = SettingsService.Settings.AutoRestorePointEnabled;
            await LoadRestorePointsAsync();
            ListPoints.ItemsSource = _points;
        }

        private void ToggleAutoRestore_Changed(object sender, RoutedEventArgs e)
        {
            if (ToggleAutoRestore == null) return;
            SettingsService.Settings.AutoRestorePointEnabled = ToggleAutoRestore.IsChecked == true;
            SettingsService.SaveSettings();
        }

        private async Task LoadRestorePointsAsync()
        {
            _points.Clear();
            try
            {
                var list = await BackupService.GetRestorePointsAsync();
                // Sort descending by sequence number (most recent first)
                foreach (var p in list.OrderByDescending(x => x.SequenceNumber))
                {
                    _points.Add(p);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading restore points: {ex.Message}");
            }
        }

        private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtBackupName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowMessage("⚠️ Ingresa un nombre para el punto de restauración.", isError: true);
                return;
            }

            BtnCreateBackup.IsEnabled = false;
            var oldContent = BtnCreateBackup.Content;
            BtnCreateBackup.Content = "Creando punto (tarda ~1 min)...";
            TxtMessage.Visibility = Visibility.Collapsed;

            try
            {
                bool success = await BackupService.CreateRestorePointAsync(name);
                if (success)
                {
                    ShowMessage($"✨ Punto de restauración \"{name}\" creado con éxito.", isError: false);
                    TxtBackupName.Text = string.Empty;
                    await LoadRestorePointsAsync();
                }
                else
                {
                    ShowMessage("⚠️ Error al crear punto de restauración. Asegúrate de tener la protección del sistema activa en la unidad C:.", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"⚠️ Excepción al crear el punto: {ex.Message}", isError: true);
            }
            finally
            {
                BtnCreateBackup.IsEnabled = true;
                BtnCreateBackup.Content = oldContent;
            }
        }

        private void BtnOpenRestoreUI_Click(object sender, RoutedEventArgs e)
        {
            BackupService.OpenSystemRestoreUI();
        }

        private async void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConfirmingDelete)
            {
                // Enter confirmation state
                _isConfirmingDelete = true;
                BtnDeleteAll.Content = "¿Seguro? Clic de nuevo para borrar";
                BtnDeleteAll.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                BtnDeleteAll.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                BtnDeleteAll.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

                // Cancel confirmation if user clicks away or after 5 seconds
                await Task.Delay(5000);
                if (_isConfirmingDelete)
                {
                    ResetDeleteButton();
                }
            }
            else
            {
                // Execute deletion
                _isConfirmingDelete = false;
                BtnDeleteAll.IsEnabled = false;
                BtnDeleteAll.Content = "Eliminando copias...";
                TxtMessage.Visibility = Visibility.Collapsed;

                try
                {
                    bool success = await BackupService.DeleteAllRestorePointsAsync();
                    if (success)
                    {
                        ShowMessage("✨ Todos los puntos de restauración e imágenes de volumen (Shadow Copies) han sido eliminados.", isError: false);
                        await LoadRestorePointsAsync();
                    }
                    else
                    {
                        ShowMessage("⚠️ Error al eliminar los puntos de restauración.", isError: true);
                    }
                }
                catch (Exception ex)
                {
                    ShowMessage($"⚠️ Excepción al eliminar: {ex.Message}", isError: true);
                }
                finally
                {
                    BtnDeleteAll.IsEnabled = true;
                    ResetDeleteButton();
                }
            }
        }

        private void ResetDeleteButton()
        {
            _isConfirmingDelete = false;
            BtnDeleteAll.Content = "Eliminar Todos los Puntos (Liberar Espacio)";
            BtnDeleteAll.ClearValue(Button.BackgroundProperty);
            BtnDeleteAll.ClearValue(Button.ForegroundProperty);
            BtnDeleteAll.ClearValue(Button.BorderBrushProperty);
        }

        private void ShowMessage(string text, bool isError)
        {
            TxtMessage.Text = text;
            TxtMessage.Background = isError ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0xFA, 0x52, 0x52)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, 0x10, 0x7C, 0x41));
            TxtMessage.Foreground = isError ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFA, 0x52, 0x52)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0xC0, 0x57));
            TxtMessage.Visibility = Visibility.Visible;
        }
    }
}
