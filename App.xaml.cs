using System.Windows;
using Clipboarder.Services;

namespace Clipboarder;

public partial class App : Application
{
    private TrayService? _tray;
    private MainWindow? _main;

    // Minimum time between GitHub API calls on startup — prevents hammering the
    // release endpoint if the user restarts the app repeatedly.
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

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

        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        // Delay so the app finishes hooking ClipboardMonitor/HotkeyService first;
        // also keeps us out of the way of any login-time I/O contention.
        await Task.Delay(TimeSpan.FromSeconds(4));

        var settings = SettingsStore.Load();
        var now = DateTime.UtcNow;
        if (settings.LastUpdateCheckUtc is { } last && now - last < UpdateCheckInterval)
            return;

        var info = await UpdateService.CheckAsync();
        SettingsStore.Update(s => s.LastUpdateCheckUtc = now);

        if (info is null) return;
        if (string.Equals(settings.SkippedUpdateTag, info.TagName, StringComparison.OrdinalIgnoreCase))
            return;

        await Dispatcher.InvokeAsync(() => PromptAndInstall(info));
    }

    private async void PromptAndInstall(UpdateInfo info)
    {
        if (_main is null) return;

        var current = UpdateService.CurrentVersion()?.ToString(3) ?? "unknown";
        var message =
            $"A new version of Clipboarder is available.\n\n" +
            $"Installed: {current}\nAvailable:  {info.Latest.ToString(3)}\n\n" +
            "Install it now? The app will briefly close and relaunch.";

        var ok = ConfirmDialog.Show(_main.IsVisible ? _main : null, "Update available", message, "Install");
        if (!ok)
        {
            // "Not now" ≈ skip this specific version; user will see the next release.
            SettingsStore.Update(s => s.SkippedUpdateTag = info.TagName);
            return;
        }

        _main.VM.ShowToastMessage($"Downloading {info.Latest.ToString(3)}…");
        var path = await UpdateService.DownloadAsync(info.DownloadUrl);
        if (path is null)
        {
            _main.VM.ShowToastMessage("Update download failed");
            return;
        }

        _main.VM.ShowToastMessage("Installing update…");
        if (!UpdateService.LaunchInstaller(path))
        {
            _main.VM.ShowToastMessage("Couldn't launch installer");
            return;
        }

        // Give the installer a moment to spawn before we let ourselves be closed by it.
        await Task.Delay(400);
        QuitApp();
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
