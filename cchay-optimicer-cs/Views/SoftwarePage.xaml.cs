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
        private bool _isInitialized = false;

        public SoftwarePage()
        {
            InitializeComponent();
            InitializeSoftwareList();
            _isInitialized = true;
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
                new SoftwarePackage { Id = "Adobe.Acrobat.Reader.64-bit", Name = "Adobe Acrobat Reader", Description = "Lector estándar de archivos PDF líder en la industria.", Category = "Utilidades" },
                new SoftwarePackage { Id = "LibreOffice.LibreOffice", Name = "LibreOffice", Description = "Suite de oficina gratuita y de código abierto compatible con Word, Excel y PowerPoint.", Category = "Utilidades" },
                new SoftwarePackage { Id = "Notepad++.Notepad++", Name = "Notepad++", Description = "Editor de código y texto ligero, rápido y potente.", Category = "Utilidades" },
                new SoftwarePackage { Id = "WinSCP.WinSCP", Name = "WinSCP", Description = "Cliente SFTP y FTP gráfico para transferencias seguras de archivos.", Category = "Utilidades" },
                new SoftwarePackage { Id = "JAMSoftware.TreeSize.Free", Name = "TreeSize Free", Description = "Analizador de espacio en disco rápido para identificar qué carpetas pesan más.", Category = "Utilidades" },
                new SoftwarePackage { Id = "RevoUninstaller.RevoUninstaller", Name = "Revo Uninstaller", Description = "Desinstalador de programas avanzado que borra archivos y registros residuales.", Category = "Utilidades" },
                
                // Comunicación & Entretenimiento
                new SoftwarePackage { Id = "Discord.Discord", Name = "Discord", Description = "Plataforma de chat de voz, video y texto popular entre gamers.", Category = "Comunicación" },
                new SoftwarePackage { Id = "Spotify.Spotify", Name = "Spotify", Description = "Servicio de música digital que te da acceso a millones de canciones.", Category = "Multimedia" },
                new SoftwarePackage { Id = "Zoom.Zoom", Name = "Zoom", Description = "Software de videoconferencia para reuniones y chats de equipo.", Category = "Comunicación" },
                
                // Gaming
                new SoftwarePackage { Id = "Valve.Steam", Name = "Steam Launcher", Description = "La tienda y lanzador oficial de videojuegos de Valve.", Category = "Gaming" },
                new SoftwarePackage { Id = "EpicGames.EpicGamesLauncher", Name = "Epic Games Launcher", Description = "Lanzador de juegos de Epic Games. Juegos gratuitos semanales.", Category = "Gaming" },

                // Runtimes
                new SoftwarePackage { Id = "Microsoft.VCRedist.2015+.x64", Name = "Visual C++ Redistributable 2015-2022", Description = "Componente del sistema necesario para ejecutar juegos y aplicaciones compiladas en C++.", Category = "Runtimes" },
                new SoftwarePackage { Id = "Microsoft.DotNet.DesktopRuntime.8", Name = ".NET Desktop Runtime 8.0", Description = "Entorno de ejecución de Microsoft .NET necesario para aplicaciones WPF/Windows Forms.", Category = "Runtimes" },
                new SoftwarePackage { Id = "Oracle.JavaRuntimeEnvironment", Name = "Java Runtime JRE", Description = "Entorno de ejecución de Java imprescindible para apps empresariales y juegos como Minecraft.", Category = "Runtimes" },
                new SoftwarePackage { Id = "Microsoft.DirectX", Name = "DirectX End-User Runtime", Description = "Colección de APIs multimedia para aceleración de gráficos 3D y audio.", Category = "Runtimes" },

                // Diagnóstico
                new SoftwarePackage { Id = "HWiNFO.HWiNFO", Name = "HWiNFO", Description = "Diagnóstico, análisis e información de hardware completa en tiempo real.", Category = "Diagnóstico" },
                new SoftwarePackage { Id = "CrystalDiskInfo.CrystalDiskInfo", Name = "CrystalDiskInfo", Description = "Utilidad de diagnóstico para monitorear el estado de salud y temperatura de SSD/HDD.", Category = "Diagnóstico" },
                new SoftwarePackage { Id = "CPUID.CPU-Z", Name = "CPU-Z", Description = "Herramienta que muestra detalles del procesador, placa base, memoria y GPU.", Category = "Diagnóstico" },
                new SoftwarePackage { Id = "MSI.Afterburner", Name = "MSI Afterburner", Description = "Software de monitorización del hardware y overclocking de la GPU.", Category = "Diagnóstico" },

                // Multimedia adicional
                new SoftwarePackage { Id = "OBSProject.OBSStudio", Name = "OBS Studio", Description = "Software de transmisión y grabación de pantalla libre y de código abierto.", Category = "Multimedia" }
            };

            foreach (var pkg in defaultPackages)
            {
                _allSoftware.Add(new SoftwareViewModel(pkg));
            }

            ListSoftware.ItemsSource = _allSoftware;
        }

        private void ApplyFilters()
        {
            if (!_isInitialized) return;

            string query = TxtSearch.Text.Trim().ToLower();
            string selectedCategory = (ComboCategory.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todas las Categorías";

            var filtered = _allSoftware.AsEnumerable();

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(s => s.Name.ToLower().Contains(query) || s.Description.ToLower().Contains(query));
            }

            if (selectedCategory != "Todas las Categorías")
            {
                filtered = filtered.Where(s => s.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            ListSoftware.ItemsSource = filtered.ToList();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ComboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            var visibleItems = ListSoftware.ItemsSource as List<SoftwareViewModel>;
            if (visibleItems != null)
            {
                foreach (var item in visibleItems)
                {
                    item.IsChecked = true;
                }
            }
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            var visibleItems = ListSoftware.ItemsSource as List<SoftwareViewModel>;
            if (visibleItems != null)
            {
                foreach (var item in visibleItems)
                {
                    item.IsChecked = false;
                }
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
