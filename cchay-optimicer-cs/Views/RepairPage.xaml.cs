using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class RepairPage : UserControl
    {
        private bool _isRunning = false;

        public RepairPage()
        {
            InitializeComponent();
        }

        private async void BtnRepair_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                MessageBox.Show("Hay otra tarea de reparación ejecutándose en este momento. Por favor espera a que termine.", "Tarea en Curso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn && btn.Tag is string commandType)
            {
                _isRunning = true;
                btn.IsEnabled = false;
                TxtLog.Clear();
                AppendLog($"=== INICIANDO TAREA DE REPARACIÓN: {commandType.ToUpper()} ===\r\n");

                try
                {
                    bool success = await RepairService.RunRepairCommandAsync(commandType, (outputLine) =>
                    {
                        Dispatcher.Invoke(() => AppendLog(outputLine));
                    });

                    if (success)
                    {
                        AppendLog("\r\n=== [OK] TAREA COMPLETADA CORRECTAMENTE ===\r\n");
                        MainWindow.Instance?.ShowRebootRequired();
                    }
                    else
                    {
                        AppendLog("\r\n=== [!] LA TAREA TERMINÓ ===\r\n");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"\r\n[ERROR EXCEPCIÓN] {ex.Message}\r\n");
                }
                finally
                {
                    btn.IsEnabled = true;
                    _isRunning = false;
                }
            }
        }

        private void AppendLog(string text)
        {
            TxtLog.AppendText(text);
            LogScroll.ScrollToEnd();
        }
    }
}
