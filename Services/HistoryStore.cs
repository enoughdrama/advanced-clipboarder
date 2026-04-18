using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clipboarder.Models;

namespace Clipboarder.Services;

public static class HistoryStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboarder");
    private static readonly string FilePath = Path.Combine(Dir, "history.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        // Skip ClipItem's computed accessors (TypeLabel, GroupKey, TypeColor, TemplateKey, Id).
        // Persist only init/set properties like Type, Content, Source, Timestamp, Pinned, etc.
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<ClipItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<ClipItem>>(json, Opts);
            return items ?? new();
        }
        catch { return new(); }
    }

    public static void Save(IEnumerable<ClipItem> items)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            // Write-then-rename avoids a truncated file if the process dies mid-write.
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(items, Opts));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }
        catch { }
    }
}
