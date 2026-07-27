using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class BenchmarkPage : UserControl
    {
        private BenchmarkResult? _lastResult;
        private CancellationTokenSource? _cts;

        public BenchmarkPage()
        {
            InitializeComponent();
            Loaded += BenchmarkPage_Loaded;
        }

        private void BenchmarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            var baseline = BenchmarkService.GetBaseline();
            if (baseline != null)
            {
                DisplayResult(baseline, "Línea base guardada anteriormente.");
            }
        }

        private async void BtnStartBenchmark_Click(object sender, RoutedEventArgs e)
        {
            BtnStartBenchmark.IsEnabled = false;
            BtnSaveBaseline.IsEnabled = false;
            ProgressSection.Visibility = Visibility.Visible;
            BenchProgressBar.Value = 0;
            TxtStatus.Text = "Evaluando rendimiento de Disco, RAM y Procesador...";

            _cts = new CancellationTokenSource();
            var progress = new Progress<double>(val =>
            {
                BenchProgressBar.Value = val;
            });

            try
            {
                _lastResult = await BenchmarkService.RunBenchmarkAsync(_cts.Token, progress);
                var baseline = BenchmarkService.GetBaseline();

                string improvementText = "";
                if (baseline != null && baseline.TotalScore > 0)
                {
                    double diffPercent = ((double)_lastResult.TotalScore - baseline.TotalScore) / baseline.TotalScore * 100;
                    if (diffPercent >= 0)
                    {
                        improvementText = $"🚀 ¡Rendimiento +{diffPercent:F1}% superior comparado con tu línea base previa!";
                    }
                    else
                    {
                        improvementText = $"Puntaje actual: {_lastResult.TotalScore} pts. (Línea base previa: {baseline.TotalScore} pts).";
                    }
                }
                else
                {
                    improvementText = "Puntaje obtenido exitosamente. Puedes guardarlo como tu línea base.";
                }

                DisplayResult(_lastResult, improvementText);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error durante el benchmark: {ex.Message}";
            }
            finally
            {
                ProgressSection.Visibility = Visibility.Collapsed;
                BtnStartBenchmark.IsEnabled = true;
                BtnSaveBaseline.IsEnabled = true;
            }
        }

        private void DisplayResult(BenchmarkResult res, string message)
        {
            TxtScore.Text = res.TotalScore.ToString();
            TxtGrade.Text = res.Grade;
            TxtDiskSpeed.Text = $"{res.DiskSpeedMBs:F1} MB/s";
            TxtRamLatency.Text = $"{res.RamLatencyMs:F1} ms";
            TxtCpuResponse.Text = $"{res.CpuResponseMs:F1} ms";
            TxtImprovement.Text = message;
        }

        private void BtnSaveBaseline_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult != null)
            {
                BenchmarkService.SaveBaseline(_lastResult);
                MessageBox.Show("Puntaje actual guardado exitosamente como Línea Base.", "Benchmark", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Primero debes ejecutar el benchmark para guardar el puntaje.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
