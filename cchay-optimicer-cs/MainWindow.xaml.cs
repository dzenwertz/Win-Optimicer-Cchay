using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using cchay_optimicer_cs.Services;
using cchay_optimicer_cs.Views;
using Wpf.Ui;

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
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            // Configure the PageService on NavigationView for automated resolution
            RootNavigation.SetPageService(new SimplePageService());

            Loaded += MainWindow_Loaded;
        }

        private void Log(string msg)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "navigation_log.txt");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\r\n");
            }
            catch { }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log("MainWindow Loaded");
            
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
    }
}