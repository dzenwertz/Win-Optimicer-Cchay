using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using cchay_optimicer_cs.Models;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public class DnsProviderViewModel : INotifyPropertyChanged
    {
        private readonly DnsProvider _provider;
        private string _statusText = "Aplicar";
        private string _pingText = "";
        private Visibility _pingVisibility = Visibility.Collapsed;
        private Brush _pingColor = Brushes.Gray;

        public DnsProviderViewModel(DnsProvider provider)
        {
            _provider = provider;
        }

        public string Key => _provider.Key;
        public string Name => _provider.Name;
        public string Description => _provider.Description;
        public string Primary => _provider.Primary;
        public string Secondary => _provider.Secondary;

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string PingText
        {
            get => _pingText;
            set
            {
                _pingText = value;
                OnPropertyChanged(nameof(PingText));
            }
        }

        public Brush PingColor
        {
            get => _pingColor;
            set
            {
                _pingColor = value;
                OnPropertyChanged(nameof(PingColor));
            }
        }

        public Visibility PingVisibility
        {
            get => _pingVisibility;
            set
            {
                _pingVisibility = value;
                OnPropertyChanged(nameof(PingVisibility));
            }
        }

        public void UpdatePing(int? ping)
        {
            _provider.Ping = ping;
            if (ping == null)
            {
                PingText = "";
                PingVisibility = Visibility.Collapsed;
                PingColor = Brushes.Gray;
            }
            else
            {
                PingText = $"{ping} ms";
                PingVisibility = Visibility.Visible;
                if (ping < 50)
                {
                    PingColor = (Brush)new BrushConverter().ConvertFromString("#40C057")!;
                }
                else if (ping < 150)
                {
                    PingColor = (Brush)new BrushConverter().ConvertFromString("#FFA94D")!;
                }
                else
                {
                    PingColor = (Brush)new BrushConverter().ConvertFromString("#FA5252")!;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class NetworkPage : UserControl
    {
        private readonly ObservableCollection<DnsProviderViewModel> _dnsProviders = new ObservableCollection<DnsProviderViewModel>();
        private bool _isInitialized = false;

        public NetworkPage()
        {
            InitializeComponent();
            Loaded += NetworkPage_Loaded;
        }

        private void NetworkPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;

            // Load TCP tweaks status
            var state = NetworkService.LoadNetworkState();
            ToggleTcp.IsChecked = state.TcpTweaksEnabled;

            // Load DNS list
            var providers = NetworkService.GetDnsProviders();
            _dnsProviders.Clear();
            foreach (var p in providers)
            {
                _dnsProviders.Add(new DnsProviderViewModel(p));
            }

            // Detect current DNS
            var activeDns = GetActiveDnsAddresses();
            UpdateDnsStatus(activeDns);

            ListDNS.ItemsSource = _dnsProviders;
            _isInitialized = true;
        }

        private async void ToggleTcp_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool isChecked = ToggleTcp.IsChecked == true;
            bool success = await NetworkService.ApplyTcpTweaksAsync(isChecked);

            if (success)
            {
                ShowToast(isChecked ? "¡Optimizaciones TCP aplicadas con éxito!" : "¡Optimizaciones TCP revertidas por defecto!");
                MainWindow.Instance?.ShowRebootRequired();
            }
            else
            {
                ShowToast("Error al aplicar las optimizaciones TCP.");
            }
        }

        private async void BtnPingTest_Click(object sender, RoutedEventArgs e)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show(
                    "No se detectó ninguna interfaz de red activa en el equipo.\n\nPor favor, verifica tu conexión a Internet o activa el adaptador de red.",
                    "Sin Conexión de Red",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            BtnPingTest.IsEnabled = false;
            TxtPingBtn.Text = "Testeando...";

            try
            {
                var tested = await NetworkService.TestAllPingsAsync();
                
                // Clear and re-populate sorted by ping
                _dnsProviders.Clear();
                foreach (var p in tested)
                {
                    var vm = new DnsProviderViewModel(p);
                    vm.UpdatePing(p.Ping);
                    _dnsProviders.Add(vm);
                }

                // Restore applied/active statuses
                var activeDns = GetActiveDnsAddresses();
                UpdateDnsStatus(activeDns);

                ShowToast("¡Test de velocidad completado!");
            }
            catch (Exception ex)
            {
                ShowToast($"Error al testear pings: {ex.Message}");
            }
            finally
            {
                BtnPingTest.IsEnabled = true;
                TxtPingBtn.Text = "Test de Velocidad DNS";
            }
        }

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            BtnFlushDns.IsEnabled = false;
            TxtFlushBtn.Text = "Vaciando...";

            try
            {
                bool success = await NetworkService.FlushDnsAsync();
                if (success)
                {
                    ShowToast("¡Caché de resolución DNS (Flush) vaciada!");
                }
                else
                {
                    ShowToast("No se pudo vaciar la caché DNS.");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error: {ex.Message}");
            }
            finally
            {
                BtnFlushDns.IsEnabled = true;
                TxtFlushBtn.Text = "Vaciar Caché (Flush)";
            }
        }

        private async void BtnResetDns_Click(object sender, RoutedEventArgs e)
        {
            BtnResetDns.IsEnabled = false;
            TxtResetBtn.Text = "Restableciendo...";

            try
            {
                bool success = await NetworkService.ResetDnsAsync();
                if (success)
                {
                    var activeDns = GetActiveDnsAddresses();
                    UpdateDnsStatus(activeDns);
                    ShowToast("¡Configuración de DNS restablecida a DHCP!");
                }
                else
                {
                    ShowToast("No se pudo restablecer la configuración de DNS.");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error: {ex.Message}");
            }
            finally
            {
                BtnResetDns.IsEnabled = true;
                TxtResetBtn.Text = "Restablecer (DHCP)";
            }
        }

        private async void BtnApplyDns_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
            {
                var vm = _dnsProviders.FirstOrDefault(p => p.Key == key);
                if (vm == null) return;

                btn.IsEnabled = false;
                var originalContent = btn.Content;
                btn.Content = "Aplicando...";

                try
                {
                    // Create system restore point first for safety
                    if (SettingsService.Settings.AutoRestorePointEnabled)
                    {
                        await BackupService.CreateRestorePointAsync($"Cchay DNS Config {vm.Name}");
                    }

                    bool success = await NetworkService.SetDnsAsync(new[] { vm.Primary, vm.Secondary });
                    if (success)
                    {
                        var activeDns = GetActiveDnsAddresses();
                        UpdateDnsStatus(activeDns);
                        ShowToast($"¡DNS de {vm.Name} aplicada con éxito!");
                    }
                    else
                    {
                        ShowToast("Error al configurar la dirección DNS.");
                    }
                }
                catch (Exception ex)
                {
                    ShowToast($"Error: {ex.Message}");
                }
                finally
                {
                    btn.IsEnabled = true;
                    btn.Content = originalContent;
                }
            }
        }

        private void UpdateDnsStatus(List<string> activeDnsAddresses)
        {
            foreach (var vm in _dnsProviders)
            {
                bool isActive = activeDnsAddresses.Contains(vm.Primary) || activeDnsAddresses.Contains(vm.Secondary);
                vm.StatusText = isActive ? "Aplicado" : "Aplicar";
            }
        }

        private List<string> GetActiveDnsAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up && 
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(nic => nic.GetIPProperties().DnsAddresses)
                    .Select(addr => addr.ToString())
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
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

        private async void BtnRunSpeedTest_Click(object sender, RoutedEventArgs e)
        {
            BtnRunSpeedTest.IsEnabled = false;
            TxtSpeedStatus.Text = "Pinguando...";
            TxtSpeedStatus.Foreground = Brushes.Orange;
            ProgSpeedTest.Progress = 0;
            TxtSpeedValue.Text = "0.0";
            TxtSpeedPing.Text = "-- ms";
            TxtSpeedDownload.Text = "-- Mbps";

            try
            {
                // Measure Ping
                int pingTime = 999;
                using (var ping = new Ping())
                {
                    try
                    {
                        var reply = await ping.SendPingAsync("1.1.1.1", 1000);
                        if (reply.Status == IPStatus.Success)
                        {
                            pingTime = (int)reply.RoundtripTime;
                            TxtSpeedPing.Text = $"{pingTime} ms";
                        }
                    }
                    catch
                    {
                        TxtSpeedPing.Text = "Fallo";
                    }
                }

                // Measure Download Speed
                TxtSpeedStatus.Text = "Descargando...";
                TxtSpeedStatus.Foreground = Brushes.Cyan;

                string testUrl = "https://speed.cloudflare.com/__down?bytes=5000000"; // 5MB test file
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var stopwatch = new Stopwatch();
                    
                    stopwatch.Start();
                    using (var response = await client.GetAsync(testUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength ?? 5000000;
                        
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            byte[] buffer = new byte[65536]; // 64KB chunks
                            long totalRead = 0;
                            int read;
                            
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                totalRead += read;
                                double progress = (double)totalRead / totalBytes.Value * 100;
                                
                                double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                                double currentMbps = elapsedSec > 0 ? (totalRead * 8.0 / 1024.0 / 1024.0) / elapsedSec : 0;

                                Dispatcher.Invoke(() =>
                                {
                                    ProgSpeedTest.Progress = Math.Min(100, progress);
                                    TxtSpeedValue.Text = $"{currentMbps:F1}";
                                    TxtSpeedDownload.Text = $"{currentMbps:F1} Mbps";
                                });
                            }
                        }
                    }
                    stopwatch.Stop();

                    double finalElapsedSec = stopwatch.Elapsed.TotalSeconds;
                    double finalMbps = finalElapsedSec > 0 ? (5000000.0 * 8.0 / 1024.0 / 1024.0) / finalElapsedSec : 0;

                    TxtSpeedValue.Text = $"{finalMbps:F1}";
                    TxtSpeedDownload.Text = $"{finalMbps:F1} Mbps";
                    TxtSpeedStatus.Text = "Completado";
                    TxtSpeedStatus.Foreground = (Brush)new BrushConverter().ConvertFromString("#40C057")!;
                }
            }
            catch (Exception ex)
            {
                TxtSpeedStatus.Text = "Error";
                TxtSpeedStatus.Foreground = Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"Speed test failed: {ex.Message}");
            }
            finally
            {
                BtnRunSpeedTest.IsEnabled = true;
            }
        }
    }
}
