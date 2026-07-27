using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System;
using System.Threading.Tasks;

namespace cchay_optimicer_cs;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static System.Threading.Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Global\\CchayOptimicer_SingleInstance_Mutex";
        _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("Cchay Optimicer ya se está ejecutando (revisa los íconos ocultos junto al reloj de Windows).", "Cchay Optimicer", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        base.OnStartup(e);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Prevent background tasks from crashing the entire app
        e.SetObserved();
        System.Diagnostics.Debug.WriteLine($"[Cchay] Unobserved task exception caught: {e.Exception?.Message}");
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Ignore DllNotFoundException during shutdown (known .NET single-file bug with System.Management)
        if (e.Exception is DllNotFoundException)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;
        LogException(e.Exception);
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            // Ignore DllNotFoundException during shutdown — false positive from native C++ runtime cleanup
            if (ex is DllNotFoundException) return;
            LogException(ex);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string crashDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CchayOptimicer");
            Directory.CreateDirectory(crashDir);
            string crashPath = Path.Combine(crashDir, "crash.txt");
            string log = $"[{DateTime.Now}] Crash Exception:\n{ex.ToString()}\n";
            if (ex.InnerException != null)
            {
                log += $"\nInner Exception:\n{ex.InnerException.ToString()}\n";
            }
            File.AppendAllText(crashPath, log + "\n---\n");
            MessageBox.Show($"Cchay Optimicer ha detectado un error y debe cerrarse:\n\n{ex.Message}\n\nDetalles guardados en crash.txt", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}


