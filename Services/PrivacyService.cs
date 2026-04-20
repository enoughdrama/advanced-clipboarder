using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clipboarder.Services;

// Screen-capture opt-out: tells Windows (DWM) to render the window as pure
// black in any capture surface — Discord / Teams / Zoom / OBS / GDI
// screenshots all end up with a blank rectangle where Clipboarder was.
// WDA_EXCLUDEFROMCAPTURE is Windows 10 2004+; older builds fall back to
// WDA_MONITOR, which still stops most consumer-grade screen recorders.
public static class PrivacyService
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private const uint WDA_NONE                = 0x0;
    private const uint WDA_MONITOR             = 0x1;
    private const uint WDA_EXCLUDEFROMCAPTURE  = 0x11;

    public static void Apply(Window window, bool hide)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (!hide) { SetWindowDisplayAffinity(hwnd, WDA_NONE); return; }
        if (!SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
            SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
    }

    // Convenience for owned windows that open after main load — hook into
    // SourceInitialized and re-apply the current setting.
    public static void ApplyFromSettings(Window window)
    {
        var on = SettingsStore.Load().HideFromScreenCapture == true;
        Apply(window, on);
    }
}
