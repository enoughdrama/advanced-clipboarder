using System.IO;
using System.Text.Json;

namespace Clipboarder.Services;

public sealed class AppSettings
{
    public string? LastCategoryId { get; set; }

    // Auto-updater: skip prompting again for a version the user said "not now" to.
    public string? SkippedUpdateTag { get; set; }
    // Throttle API calls so startup isn't a GitHub ping every launch.
    public DateTime? LastUpdateCheckUtc { get; set; }

    // Per-app capture rules. Process-name prefixes that bypass the capture pipeline
    // entirely (matched with StartsWith, case-insensitive). `null` = use the built-in
    // defaults (password managers); an empty list = block nothing.
    public List<string>? BlockedProcesses { get; set; }

    // Content-pattern block list (full-match regexes, trimmed). Runs after the
    // process check, against the clipboard text. `null` = use defaults (credit
    // cards, SSN, AWS/GitHub/Slack tokens); `[]` = nothing.
    public List<string>? BlockedPatterns { get; set; }

    // Retention — auto-delete non-pinned items to keep the history lean and
    // minimize the window where a sensitive clip lingers on disk.
    //   TwoFactorTtlSeconds — items with Tag="2FA" (auto-detected OTP codes).
    //                         Default 60s, 0 keeps them forever.
    //   UnpinnedTtlDays     — general cap on how long any non-pinned item lives.
    //                         Default 0 = keep forever (opt-in behaviour).
    //   MaxUnpinnedItems    — hard count cap; oldest non-pinned get evicted.
    //                         Default 0 = no cap.
    public int? TwoFactorTtlSeconds { get; set; }
    public int? UnpinnedTtlDays     { get; set; }
    public int? MaxUnpinnedItems    { get; set; }

    // Global hotkey string ("Ctrl+Shift+V"). Parsed via HotkeyParser. Null =
    // use the built-in default, anything else is treated as the user's intent.
    public string? OpenWindowHotkey { get; set; }

    // When true, the app's windows are excluded from screen-capture surfaces
    // (Teams / Zoom / OBS / GDI screenshots see a black rectangle). Opt-in
    // — users who need to screenshot the app for a bug report won't expect
    // it on by default. Null hydrates to false.
    public bool? HideFromScreenCapture { get; set; }

    // Toggles the rich type-specific hover peek on clip cards. Null hydrates
    // to true — the feature is useful by default, but scrolling through a
    // dense history with tooltips firing can feel noisy for some users.
    public bool? HoverPreviewEnabled { get; set; }
}

public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboarder");
    private static readonly string Path_ = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        AppSettings s;
        try
        {
            s = File.Exists(Path_)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path_)) ?? new AppSettings()
                : new AppSettings();
        }
        catch { s = new AppSettings(); }

        // First-run / newly-introduced fields materialise as their defaults on disk
        // so the user can see exactly what's being enforced and edit from there.
        // Empty collections / explicit zeros are preserved — only `null` hydrates.
        if (HydrateDefaults(s)) Save(s);
        return s;
    }

    private static bool HydrateDefaults(AppSettings s)
    {
        var dirty = false;
        if (s.BlockedProcesses is null)
        {
            s.BlockedProcesses = new List<string>(CaptureRules.DefaultBlockedProcesses);
            dirty = true;
        }
        if (s.BlockedPatterns is null)
        {
            s.BlockedPatterns = new List<string>(CaptureRules.DefaultBlockedPatterns);
            dirty = true;
        }
        if (s.TwoFactorTtlSeconds is null) { s.TwoFactorTtlSeconds = 60; dirty = true; }
        if (s.UnpinnedTtlDays     is null) { s.UnpinnedTtlDays     = 0;  dirty = true; }
        if (s.MaxUnpinnedItems    is null) { s.MaxUnpinnedItems    = 0;  dirty = true; }
        if (s.OpenWindowHotkey    is null) { s.OpenWindowHotkey    = "Ctrl+Shift+V"; dirty = true; }
        if (s.HideFromScreenCapture is null) { s.HideFromScreenCapture = false; dirty = true; }
        if (s.HoverPreviewEnabled   is null) { s.HoverPreviewEnabled   = true;  dirty = true; }
        return dirty;
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path_, json);
        }
        catch { }
    }

    // Load → mutate → save, so callers can touch one field without wiping the rest.
    public static void Update(Action<AppSettings> mutate)
    {
        var s = Load();
        mutate(s);
        Save(s);
    }
}
