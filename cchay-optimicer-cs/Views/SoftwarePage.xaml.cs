using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Models;

namespace cchay_optimicer_cs.Views
{
    public class SoftwareViewModel : INotifyPropertyChanged
    {
        private bool _isChecked;
        private string _status = "Pendiente";

        public SoftwarePackage Package { get; }

        public SoftwareViewModel(SoftwarePackage package)
        {
            Package = package;
            _isChecked = package.IsChecked;
            _status = package.Status;
        }

        public string Id => Package.Id;
        public string Name => Package.Name;
        public string Description => Package.Description;
        public string Category => Package.Category;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                Package.IsChecked = value;
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                Package.Status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string StatusColor => Status switch
        {
            "Pendiente" => "#868E96",
            "Instalando..." => "#1098AD",
            "Instalado" => "#40C057",
            "Fallido" => "#FA5252",
            _ => "#868E96"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class SoftwarePage : UserControl
    {
        private readonly List<SoftwareViewModel> _allSoftware = new List<SoftwareViewModel>();

        public SoftwarePage()
        {
            InitializeComponent();
            InitializeSoftwareList();
        }

        private void InitializeSoftwareList()
        {
            var defaultPackages = new List<SoftwarePackage>
            {
                // Navegadores
                new SoftwarePackage { Id = "Brave.Brave", Name = "Brave Browser", Description = "Navegador web rápido enfocado en privacidad y bloqueo de anuncios.", Category = "Navegadores" },
                new SoftwarePackage { Id = "Google.Chrome", Name = "Google Chrome", Description = "El navegador web de Google más utilizado a nivel mundial.", Category = "Navegadores" },
                new SoftwarePackage { Id = "Mozilla.Firefox", Name = "Mozilla Firefox", Description = "Navegador web de código abierto, seguro y altamente personalizable.", Category = "Navegadores" },
                
                // Utilidades
                new SoftwarePackage { Id = "7zip.7zip", Name = "7-Zip", Description = "Compresor y descompresor de archivos de alta tasa de compresión y gratuito.", Category = "Utilidades" },
                new SoftwarePackage { Id = "RARLab.WinRAR", Name = "WinRAR", Description = "Herramienta popular para abrir y crear archivos comprimidos RAR y ZIP.", Category = "Utilidades" },
                new SoftwarePackage { Id = "qBittorrent.qBittorrent", Name = "qBittorrent", Description = "Cliente de Torrent libre y de código abierto sin publicidad.", Category = "Utilidades" },
                new SoftwarePackage { Id = "VideoLAN.VLC", Name = "VLC Media Player", Description = "Reproductor multimedia que soporta prácticamente cualquier formato de audio y video.", Category = "Multimedia" },
                
                // Comunicación & Entretenimiento
                new SoftwarePackage { Id = "Discord.Discord", Name = "Discord", Description = "Plataforma de chat de voz, video y texto popular entre gamers.", Category = "Comunicación" },
                new SoftwarePackage { Id = "Spotify.Spotify", Name = "Spotify", Description = "Servicio de música digital que te da acceso a millones de canciones.", Category = "Multimedia" },
                new SoftwarePackage { Id = "Zoom.Zoom", Name = "Zoom", Description = "Software de videoconferencia para reuniones y chats de equipo.", Category = "Comunicación" },
                
                // Gaming
                new SoftwarePackage { Id = "Valve.Steam", Name = "Steam Launcher", Description = "La tienda y lanzador oficial de videojuegos de Valve.", Category = "Gaming" },
                new SoftwarePackage { Id = "EpicGames.EpicGamesLauncher", Name = "Epic Games Launcher", Description = "Lanzador de juegos de Epic Games. Juegos gratuitos semanales.", Category = "Gaming" }
            };

            foreach (var pkg in defaultPackages)
            {
                _allSoftware.Add(new SoftwareViewModel(pkg));
            }

            ListSoftware.ItemsSource = _allSoftware;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allSoftware)
            {
                item.IsChecked = true;
            }
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allSoftware)
            {
                item.IsChecked = false;
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allSoftware.Where(s => s.IsChecked).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Por favor, selecciona al menos un programa para instalar.", "Instalador de Software", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // UI State Change
            BtnInstall.IsEnabled = false;
            BtnSelectAll.IsEnabled = false;
            BtnDeselectAll.IsEnabled = false;
            CardProgress.Visibility = Visibility.Visible;
            CardLogs.Visibility = Visibility.Visible;
            TxtConsoleLog.Clear();
            ProgInstall.Maximum = selected.Count;
            ProgInstall.Value = 0;

            AppendLog("=== INICIANDO INSTALACIÓN DE SOFTWARE MASIVA ===\r\n");

            int installedCount = 0;
            foreach (var app in selected)
            {
                app.Status = "Instalando...";
                TxtCurrentApp.Text = $"Instalando: {app.Name} ({app.Id})...";
                AppendLog($"[+] Descargando e instalando {app.Name} a través de winget...\r\n");

                bool success = await Task.Run(() => RunWingetInstall(app.Id));

                if (success)
                {
                    app.Status = "Instalado";
                    AppendLog($"[OK] {app.Name} se instaló correctamente.\r\n\r\n");
                }
                else
                {
                    app.Status = "Fallido";
                    AppendLog($"[ERROR] Falló la instalación de {app.Name}.\r\n\r\n");
                }

                installedCount++;
                ProgInstall.Value = installedCount;
            }

            TxtCurrentApp.Text = "Proceso de instalación completado.";
            TxtProgressStatus.Text = "Instalación Finalizada";
            AppendLog("=== PROCESO TERMINADO ===");

            BtnInstall.IsEnabled = true;
            BtnSelectAll.IsEnabled = true;
            BtnDeselectAll.IsEnabled = true;
        }

        private bool RunWingetInstall(string packageId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install --id {packageId} --silent --accept-source-agreements --accept-package-agreements",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null) return false;

                    // Read output line by line as it installs
                    while (!process.StandardOutput.EndOfStream)
                    {
                        string line = process.StandardOutput.ReadLine() ?? "";
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            Dispatcher.Invoke(() => AppendLog($"   {line}\r\n"));
                        }
                    }

                    process.WaitForExit(180000); // 3 minutes timeout per app
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendLog($"   Excepción de ejecucion: {ex.Message}\r\n"));
                return false;
            }
        }

        private void AppendLog(string text)
        {
            TxtConsoleLog.AppendText(text);
            LogScroll.ScrollToEnd();
        }
    }
}
