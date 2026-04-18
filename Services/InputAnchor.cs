using System.Runtime.InteropServices;
using WpfPoint = System.Windows.Point;
using WpfRect  = System.Windows.Rect;

namespace Clipboarder.Services;

// Locates the caret of the currently focused text input across all processes.
// Works for native Win32 / WPF / WinForms controls; web/Electron apps generally
// don't publish a system caret and fall through to null.
public static class InputAnchor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // Returns caret position in physical screen pixels, or null if no caret is available.
    public static WpfPoint? TryGetCaret()
    {
        try
        {
            var fg = PasteService.GetForegroundWindow();
            if (fg == IntPtr.Zero) return null;

            var threadId = GetWindowThreadProcessId(fg, out _);
            if (threadId == 0) return null;

            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(threadId, ref info)) return null;

            // Prefer the caret's own host window; some apps report a caret on a child HWND.
            var host = info.hwndCaret != IntPtr.Zero ? info.hwndCaret : info.hwndFocus;
            if (host == IntPtr.Zero) return null;

            var r = info.rcCaret;
            // Empty caret rect → no caret, just focus; return null so caller can fall back.
            if (r.Right == r.Left && r.Bottom == r.Top) return null;

            var pt = new POINT { X = r.Left, Y = r.Bottom };
            if (!ClientToScreen(host, ref pt)) return null;

            return new WpfPoint(pt.X, pt.Y);
        }
        catch { return null; }
    }

    // Screen rectangle of the foreground window (physical pixels). Used as a looser
    // anchor when no caret is published — still better than center-screen.
    public static WpfRect? TryGetForegroundWindowRect()
    {
        var fg = PasteService.GetForegroundWindow();
        if (fg == IntPtr.Zero) return null;
        if (!GetWindowRect(fg, out var r)) return null;
        return new WpfRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
