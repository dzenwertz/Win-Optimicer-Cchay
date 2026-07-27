using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs.Views
{
    public partial class TelemetryPage : UserControl
    {
        public TelemetryPage()
        {
            InitializeComponent();
            Loaded += TelemetryPage_Loaded;
        }

        private async void TelemetryPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync();
        }

        private async Task RefreshStatusAsync()
        {
            var status = await TelemetryService.GetStatusAsync();

            TxtHostsStatus.Text = status.HostsBlocked ? "Bloqueado 🛡️" : "No Bloqueado";
            TxtFirewallStatus.Text = status.FirewallRulesApplied ? "Activo 🔥" : "Inactivo";
            TxtServicesStatus.Text = status.ServicesDisabled ? "Desactivados 🚫" : "Activos";

            bool isFullyProtected = status.HostsBlocked && status.ServicesDisabled;
            if (isFullyProtected)
            {
                TxtShieldStatus.Text = "Estado del Escudo: 100% PROTEGIDO";
                TxtShieldStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));
                TxtShieldDescription.Text = $"Tu privacidad está blindada contra telemetría de Windows, Edge, NVIDIA y Adobe ({status.BlockedDomainsCount}+ dominios).";
            }
            else
            {
                TxtShieldStatus.Text = "Estado del Escudo: INACTIVO / PARCIAL";
                TxtShieldStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFA, 0x52, 0x52));
                TxtShieldDescription.Text = "Presiona 'Activar Escudo Total' para aplicar las reglas de bloqueo en Hosts, Firewall y Servicios.";
            }
        }

        private async void BtnEnableShield_Click(object sender, RoutedEventArgs e)
        {
            BtnEnableShield.IsEnabled = false;
            BtnDisableShield.IsEnabled = false;
            try
            {
                await TelemetryService.EnableFullShieldAsync();
                await RefreshStatusAsync();
                MessageBox.Show("Escudo de Telemetría activado con éxito en Hosts, Firewall y Servicios.", "Escudo Total", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al activar escudo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnEnableShield.IsEnabled = true;
                BtnDisableShield.IsEnabled = true;
            }
        }

        private async void BtnDisableShield_Click(object sender, RoutedEventArgs e)
        {
            BtnEnableShield.IsEnabled = false;
            BtnDisableShield.IsEnabled = false;
            try
            {
                await TelemetryService.DisableFullShieldAsync();
                await RefreshStatusAsync();
                MessageBox.Show("Escudo de Telemetría desactivado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al desactivar escudo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnEnableShield.IsEnabled = true;
                BtnDisableShield.IsEnabled = true;
            }
        }
    }
}
