using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace CchaySetup
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.Run(new MainWindow());
        }
    }

    public class MainWindow : Window
    {
        private ProgressBar _progressBar = null!;
        private TextBlock _statusText = null!;
        private Button _btnPrimary = null!;
        private Button _btnUninstall = null!;
        private Button _btnCancel = null!;
        private bool _isAlreadyInstalled = false;
        private string _targetDir = "";
        private string _targetExe = "";

        public MainWindow()
        {
            Title = "Asistente de Instalación y Reparación - RETH Optimicer";
            Width = 540;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)); // Dark Slate

            _targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "RETH Optimicer"
            );
            _targetExe = Path.Combine(_targetDir, "RethOptimicer.exe");
            _isAlreadyInstalled = File.Exists(_targetExe);

            BuildUI();
        }

        private void BuildUI()
        {
            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var title = new TextBlock
            {
                Text = _isAlreadyInstalled ? "Actualizar / Reparar RETH Optimicer" : "Instalar RETH Optimicer",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            // Subtitle
            var subText = _isAlreadyInstalled
                ? "Se detectó una versión previa en tu sistema. Elige si deseas actualizar/reparar o desinstalar por completo."
                : "Asistente de instalación de la suite de optimización para Windows 10/11.";

            var sub = new TextBlock
            {
                Text = subText,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(sub, 1);
            grid.Children.Add(sub);

            // Center Content / Progress Panel
            var centerPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _statusText = new TextBlock
            {
                Text = _isAlreadyInstalled
                    ? "Presiona 'Actualizar / Reparar' para sobreescribir la instalación o 'Desinstalar' para removerla."
                    : "Presiona 'Instalar Ahora' para comenzar la instalación.",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            centerPanel.Children.Add(_statusText);

            _progressBar = new ProgressBar
            {
                Height = 10,
                Value = 0,
                Visibility = Visibility.Collapsed,
                Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
            };
            centerPanel.Children.Add(_progressBar);

            Grid.SetRow(centerPanel, 2);
            grid.Children.Add(centerPanel);

            // Bottom Buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _btnPrimary = new Button
            {
                Content = _isAlreadyInstalled ? "Actualizar / Reparar" : "Instalar Ahora",
                Width = 150,
                Height = 38,
                Background = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _btnPrimary.Click += BtnInstall_Click;
            btnPanel.Children.Add(_btnPrimary);

            if (_isAlreadyInstalled)
            {
                _btnUninstall = new Button
                {
                    Content = "Desinstalar",
                    Width = 110,
                    Height = 38,
                    Background = new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                _btnUninstall.Click += BtnUninstall_Click;
                btnPanel.Children.Add(_btnUninstall);
            }

            _btnCancel = new Button
            {
                Content = "Cancelar",
                Width = 90,
                Height = 38,
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            _btnCancel.Click += (s, e) => Close();
            btnPanel.Children.Add(_btnCancel);

            Grid.SetRow(btnPanel, 3);
            grid.Children.Add(btnPanel);

            Content = grid;
        }

        private static void KillRunningProcesses()
        {
            string[] procNames = { "RethOptimicer", "cchay-optimicer-cs" };
            foreach (var name in procNames)
            {
                try
                {
                    foreach (var proc in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                        }
                        catch { }
                        finally { proc.Dispose(); }
                    }
                }
                catch { }
            }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.Value = 10;
            _statusText.Text = "Deteniendo procesos activos de RETH Optimicer para liberar archivos...";

            await Task.Run(() => KillRunningProcesses());

            try
            {
                _progressBar.Value = 30;
                _statusText.Text = "Creando carpetas e instalando binarios actualizados...";
                if (!Directory.Exists(_targetDir))
                {
                    Directory.CreateDirectory(_targetDir);
                }

                // Extract embedded payload
                _progressBar.Value = 60;
                _statusText.Text = "Extrayendo payload autónomo de RETH Optimicer...";
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("Payload.exe"))
                {
                    if (stream == null) throw new Exception("No se encontró el paquete ejecutable incrustado.");
                    using var fs = new FileStream(_targetExe, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs);
                }

                // Create Desktop & Start Menu Shortcuts
                _progressBar.Value = 85;
                _statusText.Text = "Creando accesos directos en el Escritorio y Menú Inicio...";
                CreateShortcuts(_targetExe, _targetDir);

                // Register in Control Panel Uninstall
                _progressBar.Value = 95;
                _statusText.Text = "Registrando información en el Panel de Control...";
                RegisterUninstall(_targetExe, _targetDir);

                _progressBar.Value = 100;
                _statusText.Text = _isAlreadyInstalled ? "¡Actualización completada con éxito!" : "¡Instalación completada con éxito!";

                var res = MessageBox.Show(
                    _isAlreadyInstalled
                        ? "¡RETH Optimicer ha sido actualizado y reparado con éxito!\n\n¿Deseas iniciar la aplicación ahora?"
                        : "¡RETH Optimicer se ha instalado correctamente en tu equipo!\n\n¿Deseas iniciar la aplicación ahora?",
                    "Operación Exitosa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(_targetExe) { UseShellExecute = true });
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error durante la instalación/actualización: {ex.Message}", "Error de Instalación", MessageBoxButton.OK, MessageBoxImage.Error);
                SetButtonsState(true);
                _progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "¿Estás seguro de que deseas desinstalar RETH Optimicer de tu sistema?\nSe eliminarán la carpeta del programa y sus accesos directos.",
                "Confirmar Desinstalación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirm != MessageBoxResult.Yes) return;

            SetButtonsState(false);
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.Value = 20;
            _statusText.Text = "Deteniendo procesos activos de RETH Optimicer...";

            await Task.Run(() =>
            {
                KillRunningProcesses();

                // Delete shortcuts
                try
                {
                    string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RETH Optimicer.lnk");
                    if (File.Exists(desktopLink)) File.Delete(desktopLink);

                    string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "RETH Optimicer");
                    if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true);
                }
                catch { }

                // Delete Registry entry
                try
                {
                    using var hkcu = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true);
                    hkcu?.DeleteSubKeyTree("RethOptimicer", false);
                }
                catch { }

                // Delete folder
                try
                {
                    if (Directory.Exists(_targetDir))
                    {
                        Directory.Delete(_targetDir, true);
                    }
                }
                catch { }
            });

            _progressBar.Value = 100;
            _statusText.Text = "¡RETH Optimicer ha sido desinstalado completamente de tu PC!";

            MessageBox.Show(
                "¡RETH Optimicer ha sido desinstalado completamente de tu equipo!",
                "Desinstalación Completa",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            Close();
        }

        private void SetButtonsState(bool enabled)
        {
            _btnPrimary.IsEnabled = enabled;
            if (_btnUninstall != null) _btnUninstall.IsEnabled = enabled;
            _btnCancel.IsEnabled = enabled;
        }

        private static void CreateShortcuts(string targetExe, string workingDir)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType)!;

                // Desktop shortcut
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string desktopLink = Path.Combine(desktop, "RETH Optimicer.lnk");
                dynamic shortcut = shell.CreateShortcut(desktopLink);
                shortcut.TargetPath = targetExe;
                shortcut.WorkingDirectory = workingDir;
                shortcut.Description = "RETH Optimicer - Optimizador de Rendimiento de PC";
                shortcut.Save();

                // Start Menu shortcut
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "RETH Optimicer");
                if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                string startLink = Path.Combine(startMenu, "RETH Optimicer.lnk");
                dynamic startShortcut = shell.CreateShortcut(startLink);
                startShortcut.TargetPath = targetExe;
                startShortcut.WorkingDirectory = workingDir;
                startShortcut.Save();
            }
            catch { }
        }

        private static void RegisterUninstall(string targetExe, string workingDir)
        {
            try
            {
                string regPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RethOptimicer";
                using var key = Registry.CurrentUser.CreateSubKey(regPath);
                if (key != null)
                {
                    key.SetValue("DisplayName", "RETH Optimicer");
                    key.SetValue("DisplayVersion", "3.0.0");
                    key.SetValue("Publisher", "RETH");
                    key.SetValue("DisplayIcon", targetExe);
                    string uninstallCmd = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"Stop-Process -Name 'RethOptimicer' -Force -ErrorAction SilentlyContinue; Remove-Item -Path '{workingDir}' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\RethOptimicer' -Recurse -Force -ErrorAction SilentlyContinue\"";
                    key.SetValue("UninstallString", uninstallCmd);
                    key.SetValue("InstallLocation", workingDir);
                }
            }
            catch { }
        }
    }
}
