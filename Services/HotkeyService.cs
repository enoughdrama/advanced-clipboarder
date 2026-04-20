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
    public bool IsRegistered => _registered;

    public HotkeyService(Window window, ModifierKeys modifiers, Key key, int id = 0xBEEF)
    {
        _window = window; _id = id;
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle)
                  ?? throw new InvalidOperationException();
        _source.AddHook(WndProc);

        Register(helper.Handle, modifiers, key);
    }

    // Re-register the same hotkey slot with a new combination. Returns false
    // if the OS refused the registration (someone else owns that combo);
    // callers can show an error and fall back to the previous binding.
    public bool Rebind(ModifierKeys modifiers, Key key)
    {
        var helper = new WindowInteropHelper(_window);
        if (_registered)
        {
            UnregisterHotKey(helper.Handle, _id);
            _registered = false;
        }
        Register(helper.Handle, modifiers, key);
        return _registered;
    }

    private void Register(IntPtr hwnd, ModifierKeys modifiers, Key key)
    {
        uint mods = (uint)Mod.NoRepeat;
        if (modifiers.HasFlag(ModifierKeys.Alt))     mods |= (uint)Mod.Alt;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= (uint)Mod.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift))   mods |= (uint)Mod.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= (uint)Mod.Win;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(hwnd, _id, mods, vk);
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

// Round-trip between a human-readable hotkey string ("Ctrl+Shift+V") and a
// (ModifierKeys, Key) pair. Used for settings serialization and for the
// in-app recorder display.
public static class HotkeyParser
{
    public static bool TryParse(string? s, out ModifierKeys mods, out Key key)
    {
        mods = ModifierKeys.None;
        key = Key.None;
        if (string.IsNullOrWhiteSpace(s)) return false;

        foreach (var raw in s.Split('+'))
        {
            var p = raw.Trim();
            if (p.Length == 0) continue;
            switch (p.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= ModifierKeys.Control; continue;
                case "shift":                 mods |= ModifierKeys.Shift;   continue;
                case "alt":                   mods |= ModifierKeys.Alt;     continue;
                case "win": case "windows":   mods |= ModifierKeys.Windows; continue;
            }
            // Accept raw digits too (e.g. "Ctrl+Shift+1") by mapping them to Dn.
            if (p.Length == 1 && p[0] is >= '0' and <= '9')
            {
                key = (Key)((int)Key.D0 + (p[0] - '0'));
                continue;
            }
            if (Enum.TryParse<Key>(p, ignoreCase: true, out var k))
                key = k;
        }
        return key != Key.None && mods != ModifierKeys.None;
    }

    public static string Format(ModifierKeys mods, Key key)
    {
        var parts = new List<string>(4);
        if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((mods & ModifierKeys.Shift)   != 0) parts.Add("Shift");
        if ((mods & ModifierKeys.Alt)     != 0) parts.Add("Alt");
        if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
        if (key != Key.None) parts.Add(FormatKey(key));
        return string.Join("+", parts);
    }

    private static string FormatKey(Key k)
    {
        if (k >= Key.D0 && k <= Key.D9) return ((int)(k - Key.D0)).ToString();
        if (k >= Key.NumPad0 && k <= Key.NumPad9) return "Num" + (int)(k - Key.NumPad0);
        return k.ToString();
    }
}
