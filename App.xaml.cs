using System.Windows;
using Clipboarder.Services;

namespace Clipboarder;

public partial class App : Application
{
    private TrayService? _tray;
    private MainWindow? _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstance.AcquireOrSignal())
        {
            Shutdown();
            return;
        }
        base.OnStartup(e);

        AutoStartService.EnsureRegistered();

        _main = new MainWindow();
        MainWindow = _main;

        // Show/Hide forces HWND creation so ClipboardMonitor + HotkeyService can bind to it
        // before the user ever summons the window.
        _main.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _main.ShowInTaskbar = false;
        _main.Visibility = Visibility.Hidden;
        _main.Show();
        _main.Hide();

        _tray = new TrayService();
        _tray.ShowRequested  += () => Dispatcher.Invoke(() => _main.ShowFromTray());
        _tray.QuitRequested  += () => Dispatcher.Invoke(QuitApp);
        _tray.ClearRequested += () => Dispatcher.Invoke(() => _main.VM.ClearHistory());
        _tray.PauseToggled   += paused => Dispatcher.Invoke(() => _main.VM.IsCapturePaused = paused);
        _main.PauseCaptureChanged += paused => _tray.SetPaused(paused);

        SingleInstance.ListenForShowRequest(
            () => Dispatcher.Invoke(() => _main?.ShowFromTray()));
    }

    private void QuitApp()
    {
        _main?.VM.FlushPersist();
        _main?.AllowClose();
        _main?.Close();
        _tray?.Dispose();
        SingleInstance.Release();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Belt-and-braces: also flush on abnormal shutdown paths.
        try { _main?.VM.FlushPersist(); } catch { }
        _tray?.Dispose();
        SingleInstance.Release();
        base.OnExit(e);
    }
}
