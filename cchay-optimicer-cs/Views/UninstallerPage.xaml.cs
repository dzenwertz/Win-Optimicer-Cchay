using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class UninstallerPage : UserControl
    {
        private List<InstalledProgram> _allPrograms = new List<InstalledProgram>();

        public UninstallerPage()
        {
            InitializeComponent();
            Loaded += UninstallerPage_Loaded;
        }

        private async void UninstallerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProgramsAsync();
        }

        private async Task LoadProgramsAsync()
        {
            _allPrograms = await UninstallerService.GetInstalledProgramsAsync();
            FilterPrograms();
        }

        private void FilterPrograms()
        {
            string query = TxtSearch.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                ListPrograms.ItemsSource = _allPrograms;
            }
            else
            {
                ListPrograms.ItemsSource = _allPrograms
                    .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                p.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPrograms();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadProgramsAsync();
        }

        private async void ListPrograms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ListPrograms.SelectedItem as InstalledProgram;
            if (selected == null) return;

            var result = MessageBox.Show(
                $"¿Deseas desinstalar '{selected.Name}' y escanear sus archivos huérfanos residuo en el sistema?",
                "Confirmar Desinstalación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                bool launched = await UninstallerService.UninstallProgramAsync(selected);
                if (!launched)
                {
                    MessageBox.Show("No se pudo iniciar el desinstalador. Verifica que la aplicaciÃ³n siga instalada e inténtalo de nuevo.", "Error de desinstalación", MessageBoxButton.OK, MessageBoxImage.Error);
                    ListPrograms.SelectedItem = null;
                    return;
                }

                MessageBox.Show("El desinstalador se inició. Cuando termine, usa Actualizar para comprobar si quedan residuos.", "Desinstalador iniciado", MessageBoxButton.OK, MessageBoxImage.Information);
                ListPrograms.SelectedItem = null;

                // Do not delete files while an external uninstaller may still be using them.
                var waitForCompletion = MessageBox.Show(
                    "Cuando el desinstalador se haya cerrado, selecciona Sí para analizar residuos. Si todavía está abierto, selecciona No y actualiza más tarde.",
                    "Esperar la desinstalación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (waitForCompletion != MessageBoxResult.Yes) return;

                var residues = await UninstallerService.ScanOrphanResiduesAsync(selected);
                if (residues.Count > 0)
                {
                    var cleanRes = MessageBox.Show(
                        $"Se encontraron {residues.Count} carpetas huérfanas en AppData de {selected.Name}.\n¿Deseas eliminarlas ahora?",
                        "Escáner de Residuos",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                    if (cleanRes == MessageBoxResult.Yes)
                    {
                        await UninstallerService.CleanResiduesAsync(residues);
                        MessageBox.Show("Residuos eliminados exitosamente.", "Limpieza Completa", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Desinstalación finalizada sin residuos detectados.", "Completado", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadProgramsAsync();
            }

            ListPrograms.SelectedItem = null;
        }
    }
}
