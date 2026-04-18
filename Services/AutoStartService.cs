using System.Diagnostics;
using Microsoft.Win32;

namespace Clipboarder.Services;

// Registers the app under HKCU\...\Run so Windows launches it at sign-in.
// HKCU is per-user — no admin rights needed — and survives reinstall.
public static class AutoStartService
{
    private const string RunKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clipboarder";

    // Called on every startup. No-op if already registered with the current exe path,
    // so moving/updating the binary self-heals on next launch.
    public static void EnsureRegistered()
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;

            // Quote the path in case it contains spaces (e.g. "C:\Program Files\...\Clipboarder.exe").
            var desired = $"\"{exe}\"";

            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;

            var existing = key.GetValue(ValueName) as string;
            if (string.Equals(existing, desired, StringComparison.OrdinalIgnoreCase)) return;

            key.SetValue(ValueName, desired, RegistryValueKind.String);
        }
        catch { }
    }
}
