using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace Clipboarder.Services;

// Decides whether a clipboard change should be captured at all. Runs *before*
// we read clipboard content, so passwords from KeePass / 1Password / Bitwarden
// never even land in memory, let alone the encrypted history file.
//
// Two layered defences:
//   1. Standard Windows "exclusion" clipboard formats. Well-behaved password
//      managers register one or more of these on the clipboard before writing
//      the secret — the Windows cloud clipboard honours them too.
//   2. Foreground-process blocklist. Catches apps that don't set the formats
//      (older KeePass builds, browser-embedded password managers, etc.).
public static class CaptureRules
{
    // Format names documented by Microsoft and implemented by major password
    // managers. Mere presence on the clipboard is treated as "exclude me";
    // per docs, the DWORD-valued ones (CanInclude…, CanUpload…) should be 0,
    // but in the wild the formats are only ever registered to say "don't".
    private static readonly string[] ExclusionFormats =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
        "Clipboard Viewer Ignore",
    };

    // Default blocklist. Matched case-insensitively against Process.ProcessName
    // with StartsWith, so "KeePass" covers "KeePassXC" too.
    public static readonly IReadOnlyList<string> DefaultBlockedProcesses = new[]
    {
        "KeePass",
        "1Password",
        "Bitwarden",
        "LastPass",
        "Dashlane",
        "RoboForm",
        "Enpass",
        "NordPass",
        "Keeper",       // KeeperPasswordManager, KeeperDesktop, …
        "Protonpass",
        "Psono",
    };

    // Default content-pattern block list. Deliberately narrow: developer tokens
    // (AWS keys, GitHub PATs, Slack bot tokens) and region-specific IDs like SSNs
    // are NOT in here — blocking them would break legitimate dev workflows and
    // produce silent "where did my paste go?" surprises. Credit-card-shaped
    // strings stay because they rarely appear as legitimate clipboard content.
    //
    // Users who want stricter filtering can extend via `BlockedPatterns` in
    // settings.json.
    public static readonly IReadOnlyList<string> DefaultBlockedPatterns = new[]
    {
        @"^(?:\d[ -]?){13,19}$",      // credit card number (13–19 digits, optional space/dash separators)
    };

    [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    /// <summary>
    /// True if the current clipboard state should be skipped. Caller must not
    /// read clipboard content after this returns true.
    /// </summary>
    public static bool ShouldSkip(IReadOnlyList<string>? blocklist = null)
    {
        // Cheap — just metadata on the clipboard, no content read.
        if (HasExclusionFormat()) return true;

        var list = blocklist ?? DefaultBlockedProcesses;
        if (list.Count > 0 && IsSourceProcessBlocked(list)) return true;

        return false;
    }

    /// <summary>
    /// True if the given clipboard text matches a blocked content pattern
    /// (credit-card-like, SSN, API tokens, anything the user configured).
    /// Run after ShouldSkip — this one needs the text itself.
    /// </summary>
    public static bool MatchesBlockedPattern(string text, IReadOnlyList<Regex>? patterns)
    {
        if (patterns is null || patterns.Count == 0) return false;
        if (string.IsNullOrEmpty(text)) return false;
        var trimmed = text.Trim();
        foreach (var re in patterns)
        {
            try { if (re.IsMatch(trimmed)) return true; }
            catch { /* bad user pattern — ignore rather than crash the capture loop */ }
        }
        return false;
    }

    /// <summary>
    /// Compile a list of pattern strings into regexes. Silently drops invalid
    /// entries so a single bad user pattern can't take the whole filter out.
    /// </summary>
    public static IReadOnlyList<Regex> CompilePatterns(IEnumerable<string>? patterns)
    {
        if (patterns is null) return Array.Empty<Regex>();
        var list = new List<Regex>();
        foreach (var p in patterns)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            try { list.Add(new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant)); }
            catch { /* skip invalid pattern */ }
        }
        return list;
    }

    public static IReadOnlyList<Regex> CompileDefaultPatterns() => CompilePatterns(DefaultBlockedPatterns);

    private static bool HasExclusionFormat()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) return false;
            var formats = data.GetFormats(autoConvert: false);
            foreach (var f in formats)
                foreach (var excl in ExclusionFormats)
                    if (string.Equals(f, excl, StringComparison.OrdinalIgnoreCase))
                        return true;
        }
        catch { /* clipboard locked / transient — treat as allowed */ }
        return false;
    }

    private static bool IsSourceProcessBlocked(IReadOnlyList<string> blocklist)
    {
        // Clipboard *owner* is more reliable than foreground window, but some
        // apps leave it null (SetClipboardData with NULL hwnd). Fall back in
        // that case to whatever window the user was interacting with.
        var hwnd = GetClipboardOwner();
        if (hwnd == IntPtr.Zero) hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return false;

        string name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            name = p.ProcessName;
        }
        catch { return false; }

        foreach (var b in blocklist)
        {
            if (string.IsNullOrWhiteSpace(b)) continue;
            if (name.StartsWith(b, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
