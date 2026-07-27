using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class WidgetWindow : Window
    {
        private readonly DispatcherTimer _timer;

        public WidgetWindow()
        {
            InitializeComponent();
            
            // Set initial position (Top Right)
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = screenWidth - Width - 20;
            Top = 40;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateMetrics();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateMetrics();
        }

        private void UpdateMetrics()
        {
            var mem = RamService.GetMemoryUsage();
            TxtRamValue.Text = $"{mem.PercentUsed}%";

            if (mem.PercentUsed > 85)
            {
                TxtStatus.Text = "Elevado";
                TxtStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                TxtStatus.Text = "Óptimo";
                TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }

        private async void BtnQuickClean_Click(object sender, RoutedEventArgs e)
        {
            BtnQuickClean.IsEnabled = false;
            TxtStatus.Text = "Limpiando...";
            try
            {
                await RamService.CleanAll();
                UpdateMetrics();
            }
            catch { }
            finally
            {
                BtnQuickClean.IsEnabled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
