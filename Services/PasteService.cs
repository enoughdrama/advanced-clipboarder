using System.Runtime.InteropServices;

namespace Clipboarder.Services;

public static class PasteService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint Type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint Msg; public ushort ParamL, ParamH; }

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x2;
    private const uint INPUT_KEYBOARD = 1;

    public static bool IsValid(IntPtr hWnd) => hWnd != IntPtr.Zero && IsWindow(hWnd) && IsWindowVisible(hWnd);

    public static string? GetProcessName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return null;
        try
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return null; }
    }

    public static void RestoreFocusAndPaste(IntPtr targetHwnd)
    {
        if (!IsValid(targetHwnd)) return;

        // AttachThreadInput lets us bring targetHwnd to foreground reliably
        var fg = GetForegroundWindow();
        var fgThread = GetWindowThreadProcessId(fg, out _);
        var ourThread = GetCurrentThreadId();
        bool attached = false;
        if (fgThread != 0 && fgThread != ourThread)
            attached = AttachThreadInput(ourThread, fgThread, true);

        SetForegroundWindow(targetHwnd);

        if (attached) AttachThreadInput(ourThread, fgThread, false);

        Thread.Sleep(50);

        var inputs = new[]
        {
            Key(VK_CONTROL, up: false),
            Key(VK_V,       up: false),
            Key(VK_V,       up: true),
            Key(VK_CONTROL, up: true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        Type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            Keyboard = new KEYBDINPUT { Vk = vk, Flags = up ? KEYEVENTF_KEYUP : 0 }
        }
    };
}
