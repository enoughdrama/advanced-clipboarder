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
