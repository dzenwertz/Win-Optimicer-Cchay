using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;
using Forms = System.Windows.Forms;

namespace cchay_optimicer_cs.Views
{
    public partial class DiskAnalyzerPage : UserControl
    {
        private CancellationTokenSource? _cts;

        public DiskAnalyzerPage()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new Forms.FolderBrowserDialog())
            {
                folderDialog.Description = "Selecciona la carpeta a analizar";
                folderDialog.UseDescriptionForTitle = true;
                folderDialog.SelectedPath = TxtFolderPath.Text;

                if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    TxtFolderPath.Text = folderDialog.SelectedPath;
                }
            }
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtFolderPath.Text.Trim();
            if (string.IsNullOrEmpty(path)) return;

            BtnAnalyze.IsEnabled = false;
            ProgressSection.Visibility = Visibility.Visible;
            TxtStatus.Text = "Analizando archivos y subcarpetas...";

            _cts = new CancellationTokenSource();
            var progress = new Progress<double>(val =>
            {
                ScanProgressBar.IsIndeterminate = false;
                ScanProgressBar.Value = val;
            });

            try
            {
                var items = await DiskAnalyzerService.ScanDirectoryAsync(path, _cts.Token, progress);
                LstDiskItems.ItemsSource = items;
                TxtStatus.Text = $"Análisis completado. {items.Count} elementos encontrados.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error durante el análisis: {ex.Message}";
            }
            finally
            {
                ProgressSection.Visibility = Visibility.Collapsed;
                BtnAnalyze.IsEnabled = true;
            }
        }
    }
}
