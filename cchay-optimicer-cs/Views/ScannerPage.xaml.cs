using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class ScannerPage : UserControl
    {
        private readonly ObservableCollection<ThreatItem> _detectedThreats = new ObservableCollection<ThreatItem>();
        private bool _isScanning = false;

        public ScannerPage()
        {
            InitializeComponent();
            ListThreats.ItemsSource = _detectedThreats;
        }

        private void StartScanAnimation()
        {
            ElScanRing.Visibility = Visibility.Visible;
            ShieldIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Shield24;
            ShieldIcon.Foreground = (Brush)new BrushConverter().ConvertFromString("#1098AD")!;

            var anim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2.5),
                RepeatBehavior = RepeatBehavior.Forever
            };
            RotateScanRing.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        private void StopScanAnimation(bool clean)
        {
            RotateScanRing.BeginAnimation(RotateTransform.AngleProperty, null);
            ElScanRing.Visibility = Visibility.Collapsed;

            if (clean)
            {
                ShieldIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ShieldCheckmark24;
                ShieldIcon.Foreground = (Brush)new BrushConverter().ConvertFromString("#40C057")!;
            }
            else
            {
                ShieldIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ShieldDismiss24;
                ShieldIcon.Foreground = (Brush)new BrushConverter().ConvertFromString("#FA5252")!;
            }
        }

        private async void BtnQuickScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning) return;
            _isScanning = true;

            SetControlsState(false);
            _detectedThreats.Clear();
            ListThreats.Visibility = Visibility.Collapsed;
            CardNoThreats.Visibility = Visibility.Collapsed;
            CardProgress.Visibility = Visibility.Visible;
            CardActions.Visibility = Visibility.Collapsed;
            BorderToast.Visibility = Visibility.Collapsed;
            
            TxtStatusTitle.Text = "Analizando Memoria...";
            TxtStatusDesc.Text = "Revisando firmas digitales y heurísticas de procesos activos.";
            
            StartScanAnimation();

            try
            {
                var threats = await ScannerService.ScanRunningProcessesAsync((procName, percent) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgScan.Value = percent;
                        TxtCurrentTarget.Text = $"Analizando: {procName}...";
                    });
                });

                _detectedThreats.Clear();
                foreach (var t in threats)
                {
                    _detectedThreats.Add(t);
                }

                int threatCount = _detectedThreats.Count;
                TxtMetricProcessed.Text = System.Diagnostics.Process.GetProcesses().Length.ToString();
                TxtMetricThreats.Text = threatCount.ToString();
                
                if (threatCount == 0)
                {
                    TxtStatusTitle.Text = "Sistema Limpio";
                    TxtStatusDesc.Text = "No se detectaron amenazas en los procesos del sistema.";
                    TxtMetricScore.Text = "100% Seguro";
                    TxtMetricScore.Foreground = (Brush)new BrushConverter().ConvertFromString("#40C057")!;
                    CardNoThreats.Visibility = Visibility.Visible;
                    StopScanAnimation(clean: true);
                }
                else
                {
                    TxtStatusTitle.Text = "Amenazas Detectadas";
                    TxtStatusDesc.Text = $"Se encontraron {threatCount} elementos sospechosos en ejecución.";
                    TxtMetricScore.Text = "Peligro";
                    TxtMetricScore.Foreground = (Brush)new BrushConverter().ConvertFromString("#FA5252")!;
                    ListThreats.Visibility = Visibility.Visible;
                    StopScanAnimation(clean: false);
                    UpdateActionBar();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error durante el análisis rápido: {ex.Message}", isError: true);
                StopScanAnimation(clean: true);
            }
            finally
            {
                CardProgress.Visibility = Visibility.Collapsed;
                SetControlsState(true);
                _isScanning = false;
            }
        }

        private async void BtnCustomScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning) return;

            // Open folder dialog
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Seleccionar carpeta para analizar"
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedPath = dialog.FolderName;
                _isScanning = true;
                SetControlsState(false);
                _detectedThreats.Clear();
                ListThreats.Visibility = Visibility.Collapsed;
                CardNoThreats.Visibility = Visibility.Collapsed;
                CardProgress.Visibility = Visibility.Visible;
                CardActions.Visibility = Visibility.Collapsed;
                BorderToast.Visibility = Visibility.Collapsed;

                TxtStatusTitle.Text = "Analizando Carpeta...";
                TxtStatusDesc.Text = $"Escaneando {Path.GetFileName(selectedPath)} con Microsoft Defender.";
                TxtCurrentTarget.Text = $"Ruta: {selectedPath}";
                ProgScan.IsIndeterminate = true;

                StartScanAnimation();

                try
                {
                    var threats = await ScannerService.ScanPathWithDefenderAsync(selectedPath);

                    _detectedThreats.Clear();
                    foreach (var t in threats)
                    {
                        _detectedThreats.Add(t);
                    }

                    int threatCount = _detectedThreats.Count;
                    TxtMetricProcessed.Text = "1 carpeta";
                    TxtMetricThreats.Text = threatCount.ToString();

                    if (threatCount == 0)
                    {
                        TxtStatusTitle.Text = "Ruta Segura";
                        TxtStatusDesc.Text = "Microsoft Defender no encontró virus en la carpeta seleccionada.";
                        TxtMetricScore.Text = "Seguro";
                        TxtMetricScore.Foreground = (Brush)new BrushConverter().ConvertFromString("#40C057")!;
                        CardNoThreats.Visibility = Visibility.Visible;
                        StopScanAnimation(clean: true);
                        ShowToast("Análisis completado: ¡No se encontraron amenazas!", isError: false);
                    }
                    else
                    {
                        TxtStatusTitle.Text = "Amenaza Detectada";
                        TxtStatusDesc.Text = "Microsoft Defender identificó código malicioso en la ruta.";
                        TxtMetricScore.Text = "Infectado";
                        TxtMetricScore.Foreground = (Brush)new BrushConverter().ConvertFromString("#FA5252")!;
                        ListThreats.Visibility = Visibility.Visible;
                        StopScanAnimation(clean: false);
                        UpdateActionBar();
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"Error durante el análisis: {ex.Message}", isError: true);
                    StopScanAnimation(clean: true);
                }
                finally
                {
                    ProgScan.IsIndeterminate = false;
                    CardProgress.Visibility = Visibility.Collapsed;
                    SetControlsState(true);
                    _isScanning = false;
                }
            }
        }

        private async void BtnCleanSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _detectedThreats.Where(t => t.IsChecked).ToList();
            if (selected.Count == 0) return;

            BtnCleanSelected.IsEnabled = false;
            var oldText = BtnCleanSelected.Content;
            BtnCleanSelected.Content = "Desinfectando...";

            try
            {
                int successCount = 0;
                int rebootCount = 0;

                foreach (var threat in selected)
                {
                    bool success = await ScannerService.KillAndCleanThreatAsync(threat);
                    if (success)
                    {
                        // If file still exists but scheduled for reboot
                        if (File.Exists(threat.Path))
                        {
                            rebootCount++;
                        }
                        else
                        {
                            successCount++;
                        }
                        _detectedThreats.Remove(threat);
                    }
                }

                string msg = $"✨ Se eliminaron con éxito {successCount} amenazas.";
                if (rebootCount > 0)
                {
                    msg += $" {rebootCount} se programaron para borrarse al reiniciar la PC.";
                }

                ShowToast(msg, isError: false);
                
                TxtMetricThreats.Text = _detectedThreats.Count.ToString();
                if (_detectedThreats.Count == 0)
                {
                    TxtStatusTitle.Text = "Sistema Protegido";
                    TxtStatusDesc.Text = "Todas las amenazas detectadas han sido eliminadas.";
                    CardNoThreats.Visibility = Visibility.Visible;
                    ListThreats.Visibility = Visibility.Collapsed;
                    StopScanAnimation(clean: true);
                }
                else
                {
                    UpdateActionBar();
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error al eliminar amenazas: {ex.Message}", isError: true);
            }
            finally
            {
                BtnCleanSelected.IsEnabled = true;
                BtnCleanSelected.Content = oldText;
            }
        }

        private void ThreatCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActionBar();
        }

        private void UpdateActionBar()
        {
            int checkedCount = _detectedThreats.Count(t => t.IsChecked);
            if (checkedCount > 0)
            {
                TxtSelectedCount.Text = $"{checkedCount} amenazas seleccionadas";
                CardActions.Visibility = Visibility.Visible;
            }
            else
            {
                CardActions.Visibility = Visibility.Collapsed;
            }
        }

        private void SetControlsState(bool enabled)
        {
            BtnQuickScan.IsEnabled = enabled;
            BtnCustomScan.IsEnabled = enabled;
        }

        private void ShowToast(string message, bool isError)
        {
            TxtToast.Text = message;
            BorderToast.Background = isError 
                ? new SolidColorBrush(Color.FromArgb(0x1A, 0xFA, 0x52, 0x52)) 
                : new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0x7C, 0x41));
            TxtToast.Foreground = isError 
                ? new SolidColorBrush(Color.FromRgb(0xFA, 0x52, 0x52)) 
                : new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));
            BorderToast.Visibility = Visibility.Visible;
        }
    }
}
