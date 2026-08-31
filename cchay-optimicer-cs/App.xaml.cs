using System.IO;
using System.Windows;
using System.Threading.Tasks;
using cchay_optimicer_cs.Services;

namespace cchay_optimicer_cs;

public partial class App : Application
{
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Global\\RethOptimicer_SingleInstance_Mutex";
        _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show(
                "RETH Optimicer ya se está ejecutando. Revisa los iconos junto al reloj de Windows.",
                "RETH Optimicer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        JsonStorageHelper.MigrateLegacyData();

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        base.OnStartup(e);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        System.Diagnostics.Debug.WriteLine($"[RETH] Unobserved task exception: {e.Exception?.Message}");
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is DllNotFoundException)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        LogException(e.Exception, fatal: false);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            if (ex is DllNotFoundException) return;
            LogException(ex, fatal: e.IsTerminating);
        }
    }

    private void LogException(Exception ex, bool fatal)
    {
        try
        {
            string crashDir = JsonStorageHelper.AppDataDirectory;
            Directory.CreateDirectory(crashDir);
            string crashPath = Path.Combine(crashDir, "crash.txt");
            string log = $"[{DateTime.Now}] {(fatal ? "FATAL" : "UI")} Exception:\n{ex}\n";
            if (ex.InnerException != null)
            {
                log += $"\nInner Exception:\n{ex.InnerException}\n";
            }
            File.AppendAllText(crashPath, log + "\n---\n");

            string title = fatal ? "Error crítico" : "Error";
            string body = fatal
                ? $"RETH Optimicer detectó un error y debe cerrarse:\n\n{ex.Message}\n\nDetalles en crash.txt"
                : $"Se produjo un error, pero la aplicación sigue en ejecución:\n\n{ex.Message}";
            MessageBox.Show(body, title, MessageBoxButton.OK, fatal ? MessageBoxImage.Error : MessageBoxImage.Warning);
        }
        catch (Exception writeEx)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Failed to write crash log: {writeEx.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
