using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using CloudBerry.App.Models;
using CloudBerry.App.Services;
using CloudBerry.App.ViewModels;
using CloudBerry.Core.Services;

namespace CloudBerry.App;

public partial class App : Application
{
    // Static constructor fires before ANY instance construction or XAML loading.
    // This catches crashes that happen before OnStartup (e.g. XAML resource failures).
    static App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CloudBerry", "logs");
                Directory.CreateDirectory(logDir);
                var msg = args.ExceptionObject is Exception ex
                    ? $"{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}"
                    : args.ExceptionObject?.ToString() ?? "(null)";
                File.AppendAllText(
                    Path.Combine(logDir, "cloudberry.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] [pre-startup] {msg}{Environment.NewLine}");
            }
            catch { /* last resort — can't do anything if this fails */ }
        };
    }

    private readonly AppDiagnostics _diagnostics = new();
    private const int MinimumSplashDurationMilliseconds = 300;
    private static ProcessPriorityClass _currentPriorityClass = ProcessPriorityClass.Normal;

    protected override async void OnStartup(StartupEventArgs e)
    {
        SetBrowserEmulationMode();
        ConfigureDesktopPerformance();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);

        var splashViewModel = new SplashViewModel();
        var splashWindow = new SplashWindow
        {
            DataContext = splashViewModel
        };

        var startupTimer = Stopwatch.StartNew();

        try
        {
            await WindowPresentation.ShowFirstFrameAsync(splashWindow);
            _diagnostics.WriteInfo("startup-timing", $"Splash first frame: {startupTimer.ElapsedMilliseconds} ms");
            var mainViewModel = new MainViewModel();
            var progress = new Progress<StartupProgressUpdate>(splashViewModel.Apply);
            await mainViewModel.InitializeAsync(progress);
            _diagnostics.WriteInfo("startup-timing", $"Library initialized: {startupTimer.ElapsedMilliseconds} ms");

            var remainingDelay = MinimumSplashDurationMilliseconds - (int)startupTimer.ElapsedMilliseconds;
            if (remainingDelay > 0)
            {
                await Task.Delay(remainingDelay);
            }

            var mainWindow = new MainWindow(mainViewModel);
            MainWindow = mainWindow;
            mainWindow.Opacity = 0;
            await WindowPresentation.ShowFirstFrameAsync(mainWindow);
            _diagnostics.WriteInfo("startup-timing", $"Desktop first frame: {startupTimer.ElapsedMilliseconds} ms");
            splashViewModel.ProgressValue = 100;
            await Task.WhenAll(
                splashWindow.PlayCloseTransitionAsync(),
                mainWindow.PlayStartupRevealAsync());
            splashWindow.Close();
            _diagnostics.WriteInfo("startup-timing", $"Desktop ready: {startupTimer.ElapsedMilliseconds} ms");
        }
        catch (Exception exception)
        {
            _diagnostics.WriteException("startup", exception);
            splashWindow.Close();

            MessageBox.Show(
                $"Cirra couldn't finish starting up.\n\n{exception.Message}",
                "Cirra",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    private void SetBrowserEmulationMode()
    {
        try
        {
            var processName = Process.GetCurrentProcess().ProcessName + ".exe";
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);
            if (key != null)
            {
                key.SetValue(processName, 11001, RegistryValueKind.DWord);
            }
        }
        catch (Exception exception)
        {
            _diagnostics.WriteException("browser-emulation", exception);
        }
    }

    private void ConfigureDesktopPerformance()
    {
        try
        {
            RenderOptions.ProcessRenderMode = RenderMode.Default;
            Timeline.DesiredFrameRateProperty.OverrideMetadata(
                typeof(Timeline),
                new FrameworkPropertyMetadata { DefaultValue = 60 });
        }
        catch (Exception exception)
        {
            _diagnostics.WriteException("performance-rendering", exception);
        }

        try
        {
            ApplyProcessPriority(ProcessPriorityClass.AboveNormal);
        }
        catch (Exception exception)
        {
            _diagnostics.WriteException("performance-priority", exception);
        }
    }

    public static void ApplyInteractivePerformanceMode(bool isInteractive)
    {
        ApplyProcessPriority(isInteractive ? ProcessPriorityClass.AboveNormal : ProcessPriorityClass.Normal);
    }

    private static void ApplyProcessPriority(ProcessPriorityClass priorityClass)
    {
        if (_currentPriorityClass == priorityClass)
        {
            return;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = priorityClass;
            _currentPriorityClass = priorityClass;
        }
        catch
        {
            // Windows can reject priority changes under some launch contexts; keep the app running.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (MainWindow?.DataContext is MainViewModel viewModel)
            {
                viewModel.TryLaunchQueuedUpdateOnExit();
            }
        }
        catch (Exception exception)
        {
            _diagnostics.WriteException("shutdown-update", exception);
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _diagnostics.WriteException("dispatcher", e.Exception);

        if (e.Exception is InvalidOperationException &&
            (e.Exception.Message.Contains("name cannot be found in the name scope", StringComparison.OrdinalIgnoreCase) ||
             e.Exception.Message.Contains("sealed or frozen", StringComparison.OrdinalIgnoreCase)))
        {
            e.Handled = true;
            return;
        }

        // Defensive floor for transient/network exceptions. These are exactly the class of
        // failure unattended idle-time background work throws (the social presence heartbeat,
        // achievement polling, metadata refresh) when Wi-Fi drops, DNS hiccups, or Windows
        // suspends/changes network state during a long idle stretch - never something a normal
        // user action caused, and never something that leaves app state corrupted. A desktop
        // game library shouldn't crash the whole session over a dropped network call nobody
        // was watching. Genuine state-corruption bugs still fall through and crash below, same
        // as before - this only widens the safety net for exception types that are inherently
        // recoverable-by-nature, not a blanket catch-all.
        if (e.Exception is HttpRequestException ||
            e.Exception is SocketException ||
            e.Exception is TaskCanceledException ||
            e.Exception is OperationCanceledException ||
            e.Exception is System.Net.WebException)
        {
            e.Handled = true;
        }
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _diagnostics.WriteException("app-domain", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _diagnostics.WriteException("task-scheduler", e.Exception);
    }
}
