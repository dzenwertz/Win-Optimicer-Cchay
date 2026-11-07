using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using cchay_optimicer_cs.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace cchay_optimicer_cs.Views
{
    public partial class GameModePage : UserControl
    {
        private readonly DispatcherTimer _monitorTimer;
        private bool _isBusy = false;
        private bool _loadingSettings = true;
        private bool _statusRefreshRunning;

        public GameModePage()
        {
            InitializeComponent();
            _loadingSettings = false;

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;

            Loaded += GameModePage_Loaded;
            Unloaded += GameModePage_Unloaded;
        }

        private void GameModePage_Loaded(object sender, RoutedEventArgs e)
        {
            GameModeService.StateChanged += OnStateChanged;
            GameModeService.StatusUpdated += OnStatusUpdated;

            LoadSettingsIntoUI();
            UpdateUIState();
            UpdatePowerPlanAndGameStatus();

            _monitorTimer.Start();
        }

        private void GameModePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _monitorTimer.Stop();
            GameModeService.StateChanged -= OnStateChanged;
            GameModeService.StatusUpdated -= OnStatusUpdated;
        }

        private void OnStateChanged(bool isActive)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateUIState();
                UpdatePowerPlanAndGameStatus();
            });
        }

        private void OnStatusUpdated(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowNotification(message);
            });
        }

        private void LoadSettingsIntoUI()
        {
            _loadingSettings = true;
            try
            {
            var state = GameModeService.State;
            CheckPowerPlan.IsChecked = state.OptimizePowerPlan;
            CheckCpuQuantum.IsChecked = state.OptimizeCpuScheduling;
            CheckMultimedia.IsChecked = state.OptimizeMultimediaPriority;
            CheckWSearch.IsChecked = state.PauseSearchIndexing;
            CheckSysMain.IsChecked = state.PauseSysMain;
            CheckGameDvr.IsChecked = state.DisableGameDvr;
            CheckStandbyRam.IsChecked = state.CleanStandbyRamOnStart;
            CheckElevatePriority.IsChecked = state.ElevateGameProcessPriority;
            }
            finally { _loadingSettings = false; }
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings || !IsLoaded || _isBusy || GameModeService.IsActive) return;
            var state = GameModeService.State;
            state.OptimizePowerPlan = CheckPowerPlan.IsChecked ?? true;
            state.OptimizeCpuScheduling = CheckCpuQuantum.IsChecked ?? true;
            state.OptimizeMultimediaPriority = CheckMultimedia.IsChecked ?? true;
            state.PauseSearchIndexing = CheckWSearch.IsChecked ?? true;
            state.PauseSysMain = CheckSysMain.IsChecked ?? true;
            state.DisableGameDvr = CheckGameDvr.IsChecked ?? true;
            state.CleanStandbyRamOnStart = CheckStandbyRam.IsChecked ?? true;
            state.ElevateGameProcessPriority = CheckElevatePriority.IsChecked ?? true;

            GameModeService.SaveConfig();
        }

        private void UpdateUIState()
        {
            bool active = GameModeService.IsActive;
            BtnFortniteProfile.IsEnabled = !active && !_isBusy;
            foreach (var option in new[] { CheckPowerPlan, CheckCpuQuantum, CheckMultimedia,
                CheckWSearch, CheckSysMain, CheckGameDvr, CheckStandbyRam, CheckElevatePriority })
                option.IsEnabled = !active && !_isBusy;

            if (active)
            {
                CardGameModeStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57)); // Green
                BorderIcon.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x40, 0xC0, 0x57));
                IconGameMode.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));

                TxtStatusTitle.Text = "Modo Game: ACTIVO";
                TxtBadge.Text = "JUGANDO";
                BadgeStatus.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x40, 0xC0, 0x57));
                TxtBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));

                TxtStatusDesc.Text = "Optimizaciones de baja latencia aplicadas. Supervisando procesos de juegos en segundo plano.";
                TxtActiveDuration.Visibility = Visibility.Visible;

                BtnToggleGameMode.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
                IconBtnToggle.Symbol = Wpf.Ui.Controls.SymbolRegular.Stop24;
                TxtBtnToggle.Text = "Desactivar y Restaurar";
            }
            else
            {
                CardGameModeStatus.SetResourceReference(Border.BorderBrushProperty, "AccentBorderThemeBrush");
                BorderIcon.Background = new SolidColorBrush(Color.FromArgb(0x14, 0x7E, 0xD4, 0xF5));
                IconGameMode.SetResourceReference(Wpf.Ui.Controls.SymbolIcon.ForegroundProperty, "RethAccentBrush");

                TxtStatusTitle.Text = "Modo Game: Inactivo";
                TxtBadge.Text = "LISTO";
                BadgeStatus.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x7E, 0xD4, 0xF5));
                TxtBadge.SetResourceReference(TextBlock.ForegroundProperty, "RethAccentBrush");

                TxtStatusDesc.Text = "El sistema opera con la configuración estándar de Windows. Activa el modo para optimizar la respuesta.";
                TxtActiveDuration.Visibility = Visibility.Collapsed;

                BtnToggleGameMode.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                IconBtnToggle.Symbol = Wpf.Ui.Controls.SymbolRegular.Play24;
                TxtBtnToggle.Text = "Activar Modo Game";
            }
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            UpdatePowerPlanAndGameStatus();

            if (GameModeService.IsActive && GameModeService.State.ActivatedAt.HasValue)
            {
                var elapsed = DateTime.Now - GameModeService.State.ActivatedAt.Value;
                TxtActiveDuration.Text = $"Tiempo activo: {elapsed:hh\\:mm\\:ss}";
            }
        }

        private async void UpdatePowerPlanAndGameStatus()
        {
            if (_statusRefreshRunning) return;
            _statusRefreshRunning = true;
            try
            {
                var status = await Task.Run(() => (PowerSchemeHelper.GetActiveSchemeName(), GameModeService.GetActiveGameStatus()));
                if (!IsLoaded) return;
                string planName = status.Item1;
                TxtCurrentPowerPlan.Text = !string.IsNullOrWhiteSpace(planName) ? planName : "Plan estándar";

                var gameStatus = status.Item2;
                if (gameStatus.IsRunning)
                {
                    TxtGameDetected.Text = $"{gameStatus.GameName}.exe (PID: {gameStatus.Pid})";
                    TxtGameDetected.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));
                    IconGameDetected.Symbol = Wpf.Ui.Controls.SymbolRegular.Games24;
                    IconGameDetected.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0x57));
                }
                else
                {
                    TxtGameDetected.Text = "Esperando ejecución de juego...";
                    TxtGameDetected.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                    IconGameDetected.Symbol = Wpf.Ui.Controls.SymbolRegular.Desktop24;
                    IconGameDetected.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xA8));
                }
            }
            catch { }
            finally { _statusRefreshRunning = false; }
        }

        private async void BtnToggleGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            _isBusy = true;
            BtnToggleGameMode.IsEnabled = false;

            try
            {
                if (!GameModeService.IsActive)
                {
                    ShowNotification("Activando Modo Game y optimizando respuesta...");
                    var result = await GameModeService.ActivateAsync();
                    if (result.Success)
                    {
                        ShowNotification(result.Message);
                    }
                    else
                    {
                        ShowNotification($"Error: {result.Message}");
                    }
                }
                else
                {
                    ShowNotification("Desactivando Modo Game y restaurando valores previos...");
                    var result = await GameModeService.DeactivateAsync();
                    if (result.Success)
                    {
                        ShowNotification(result.Message);
                    }
                    else
                    {
                        ShowNotification($"Error: {result.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Error inesperado: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
                BtnToggleGameMode.IsEnabled = true;
                UpdateUIState();
                UpdatePowerPlanAndGameStatus();
            }
        }

        private async void BtnCleanStandbyNow_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            _isBusy = true;
            BtnCleanStandbyNow.IsEnabled = false;

            try
            {
                ShowNotification("Purgando lista de espera (Standby RAM)...");
                long freed = 0;
                freed += await RamService.CleanStandbyListLowPriority();
                freed += await RamService.CleanStandbyList();
                ShowNotification($"Standby RAM purgada: {freed / 1024.0 / 1024.0:F0} MB liberados sin afectar programas abiertos.");
            }
            catch (Exception ex)
            {
                ShowNotification($"Error al purgar RAM: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
                BtnCleanStandbyNow.IsEnabled = true;
            }
        }

        private async void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            _isBusy = true;
            BtnRestoreDefaults.IsEnabled = false;

            try
            {
                ShowNotification("Restaurando todos los parámetros a configuración de fábrica...");
                var result = await GameModeService.DeactivateAsync();
                ShowNotification(result.Message);
            }
            catch (Exception ex)
            {
                ShowNotification($"Error al restablecer: {ex.Message}");
            }
            finally
            {
                _isBusy = false;
                BtnRestoreDefaults.IsEnabled = true;
                UpdateUIState();
                UpdatePowerPlanAndGameStatus();
            }
        }

        private void ShowNotification(string message)
        {
            TxtOperationResult.Text = message;
            TxtOperationResult.Visibility = Visibility.Visible;
        }

        private void BtnFortniteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || GameModeService.IsActive) return;
            var state = GameModeService.State;
            state.OptimizePowerPlan = true;
            state.DisableGameDvr = true;
            state.ElevateGameProcessPriority = true;
            state.OptimizeCpuScheduling = false;
            state.OptimizeMultimediaPriority = false;
            state.PauseSearchIndexing = false;
            state.PauseSysMain = false;
            state.CleanStandbyRamOnStart = false;
            LoadSettingsIntoUI();
            GameModeService.SaveConfig();
            ShowNotification("Perfil seleccionado. Activa Modo Game con el cargador conectado y mide el resultado en Fortnite.");
        }
    }
}
