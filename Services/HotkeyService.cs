using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Clipboarder.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [Flags]
    private enum Mod : uint
    {
        Alt = 0x1, Control = 0x2, Shift = 0x4, Win = 0x8, NoRepeat = 0x4000,
    }

    private readonly Window _window;
    private readonly HwndSource _source;
    private readonly int _id;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyService(Window window, ModifierKeys modifiers, Key key, int id = 0xBEEF)
    {
        _window = window; _id = id;
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle)
                  ?? throw new InvalidOperationException();
        _source.AddHook(WndProc);

        uint mods = Mod.NoRepeat.HasFlag(Mod.NoRepeat) ? (uint)Mod.NoRepeat : 0u;
        if (modifiers.HasFlag(ModifierKeys.Alt))     mods |= (uint)Mod.Alt;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= (uint)Mod.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift))   mods |= (uint)Mod.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= (uint)Mod.Win;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(helper.Handle, _id, mods, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id) Pressed?.Invoke();
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            var helper = new WindowInteropHelper(_window);
            UnregisterHotKey(helper.Handle, _id);
            _registered = false;
        }
        _source.RemoveHook(WndProc);
    }
}
