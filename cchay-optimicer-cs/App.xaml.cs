using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System;

namespace cchay_optimicer_cs;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogException(e.Exception);
        Shutdown();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
    }

    private void LogException(Exception ex)
    {
        try
        {
            string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.txt");
            string log = $"[{DateTime.Now}] Crash Exception:\n{ex.ToString()}\n";
            if (ex.InnerException != null)
            {
                log += $"\nInner Exception:\n{ex.InnerException.ToString()}\n";
            }
            File.WriteAllText(crashPath, log);
            MessageBox.Show($"Cchay Optimicer ha detectado un error y debe cerrarse:\n\n{ex.Message}\n\nDetalles guardados en crash.txt", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}

