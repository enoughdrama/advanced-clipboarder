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
}

public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboarder");
    private static readonly string Path_ = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path_)) return new AppSettings();
            var json = File.ReadAllText(Path_);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
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
