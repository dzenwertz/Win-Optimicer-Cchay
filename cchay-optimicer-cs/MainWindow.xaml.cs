using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;
using cchay_optimicer_cs.Views;
using System.Threading.Tasks;
using Wpf.Ui;
using Forms = System.Windows.Forms;

namespace cchay_optimicer_cs
{
    public class SimplePageService : IPageService
    {
        private readonly Dictionary<Type, object> _pages = new Dictionary<Type, object>();

        public FrameworkElement? GetPage(Type pageType)
        {
            if (!_pages.TryGetValue(pageType, out var page))
            {
                page = Activator.CreateInstance(pageType) ?? throw new InvalidOperationException($"Could not create instance of {pageType.FullName}");
                _pages[pageType] = page;
            }
            return page as FrameworkElement;
        }

        public T? GetPage<T>() where T : class
        {
            return GetPage(typeof(T)) as T;
        }
    }

    public partial class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void ApplyDarkTitleBar()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int useDark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
                DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
            }
            catch (Exception ex)
            {
                Log($"Failed to apply dark title bar: {ex.Message}");
            }
        }

        public static MainWindow? Instance { get; private set; }
        private Forms.NotifyIcon? _notifyIcon;
        private bool _isExitingFromTray = false;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            // Configure the PageService on NavigationView for automated resolution
            RootNavigation.SetPageService(new SimplePageService());

            Loaded += MainWindow_Loaded;
            InitializeTrayIcon();
        }

        private void Log(string msg)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CchayOptimicer");
                Directory.CreateDirectory(logDir);
                string path = Path.Combine(logDir, "navigation_log.txt");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
            }
            catch { }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log("MainWindow Loaded");
            ApplyDarkTitleBar();
            
            // Check Admin Status and self-elevate if necessary
            if (!SystemService.IsRunningAsAdmin())
            {
                try
                {
                    Log("Not running as admin. Attempting self-elevation...");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cchay-optimicer-cs.exe"),
                        UseShellExecute = true,
                        Verb = "runas" // This triggers UAC elevation prompt
                    };
                    
                    Process.Start(processInfo);
                    Application.Current.Shutdown();
                    return;
                }
                catch (Exception ex)
                {
                    Log($"Self-elevation failed/cancelled: {ex.Message}");
                    // User rejected UAC prompt; run in limited mode and show warning bar
                    AdminWarningBar.Visibility = Visibility.Visible;
                }
            }
            else
            {
                AdminWarningBar.Visibility = Visibility.Collapsed;
            }

            // Start background smart RAM & maintenance service
            BackgroundMaintenanceService.Initialize();

            // Default Page
            Navigate(typeof(DashboardPage));
        }

        public void Navigate(Type pageType)
        {
            Log($"Navigate called for pageType='{pageType.Name}'");
            try
            {
                RootNavigation.Navigate(pageType);
            }
            catch (Exception ex)
            {
                Log($"FATAL: Exception navigating to '{pageType.Name}':\r\n{ex.ToString()}");
                throw;
            }
        }

        private void FooterAttribution_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/dzenwertz",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"Failed to open GitHub URL: {ex.Message}");
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Text = "Cchay Optimicer";
            
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/cchay_logo.ico");
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (var stream = streamInfo.Stream)
                    {
                        _notifyIcon.Icon = new System.Drawing.Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load tray icon: {ex.Message}");
            }

            // Create context menu
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Abrir Cchay Optimicer", null, (s, e) => RestoreWindow());
            menu.Items.Add("Limpieza Rápida de RAM", null, async (s, e) => await QuickCleanRamAsync());
            menu.Items.Add("Modo Técnico 🔥", null, async (s, e) => await TechOptimizeAsync());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Salir", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
            
            // Hook closing and state changed events
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
        }

        public void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            _isExitingFromTray = true;
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExitingFromTray && SettingsService.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                _notifyIcon?.ShowBalloonTip(3000, "Cchay Optimicer", "La aplicación sigue ejecutándose en segundo plano.", Forms.ToolTipIcon.Info);
            }
            else
            {
                _notifyIcon?.Dispose();
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && SettingsService.Settings.MinimizeToTray)
            {
                Hide();
            }
        }

        private async Task QuickCleanRamAsync()
        {
            try
            {
                long freed = await RamService.CleanAll();
                double freedMB = freed / 1024.0 / 1024.0;
                
                // Add to stats
                SettingsService.Settings.TotalBytesRamFreed += (ulong)freed;
                SettingsService.Settings.TotalOptimizationsRun++;
                SettingsService.SaveSettings();

                _notifyIcon?.ShowBalloonTip(3000, "Limpieza de RAM", $"¡Se liberaron {freedMB:F0} MB de memoria RAM!", Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        private async Task TechOptimizeAsync()
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(3000, "Modo Técnico", "Iniciando optimización completa del sistema en segundo plano...", Forms.ToolTipIcon.Info);
                
                await QuickOptimizeService.RunQuickOptimizeAsync((status, percent) => {
                    // Log progress if needed
                });

                // Add to stats
                SettingsService.Settings.TotalOptimizationsRun++;
                SettingsService.SaveSettings();

                _notifyIcon?.ShowBalloonTip(3000, "Modo Técnico", "¡Optimización técnica completada con éxito!", Forms.ToolTipIcon.Info);
            }
            catch { }
        }

        private void BtnRestartNow_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Estás seguro de que deseas reiniciar el equipo ahora? Guarda todo tu trabajo pendiente antes de continuar.", "Reiniciar Equipo", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _notifyIcon?.Dispose();
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/r /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al intentar reiniciar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDismissReboot_Click(object sender, RoutedEventArgs e)
        {
            RebootRequiredBar.Visibility = Visibility.Collapsed;
        }

        public void ShowRebootRequired()
        {
            Dispatcher.Invoke(() =>
            {
                RebootRequiredBar.Visibility = Visibility.Visible;
            });
        }
    }
}