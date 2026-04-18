using System.IO;
using System.Text.Json;

namespace Clipboarder.Services;

public sealed class AppSettings
{
    public string? LastCategoryId { get; set; }
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
}
