using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clipboarder.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private readonly HwndSource _source;
    private readonly Window _window;
    private bool _attached;

    public event Action? ClipboardChanged;

    public ClipboardMonitor(Window window)
    {
        _window = window;
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle)
                  ?? throw new InvalidOperationException("Cannot get HwndSource for window.");
        _source.AddHook(WndProc);
        if (AddClipboardFormatListener(helper.Handle)) _attached = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) ClipboardChanged?.Invoke();
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_attached)
        {
            var helper = new WindowInteropHelper(_window);
            RemoveClipboardFormatListener(helper.Handle);
            _attached = false;
        }
        _source.RemoveHook(WndProc);
    }
}
