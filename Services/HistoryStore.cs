using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clipboarder.Models;

namespace Clipboarder.Services;

// Clipboard history is sensitive (pastes can include passwords, 2FA codes,
// private tokens). We persist it with DPAPI (Windows Data Protection API)
// scoped to the current user, so only the Windows account that wrote the
// file can read it back — another local user on the same machine cannot.
//
// Format: raw DPAPI blob of the UTF-8 JSON serialisation. A domain-separation
// entropy string is mixed in so blobs from this app cannot be replayed into
// some other CurrentUser-scoped DPAPI consumer.
public static class HistoryStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboarder");
    private static readonly string FilePath   = Path.Combine(Dir, "history.dat");
    private static readonly string LegacyPath = Path.Combine(Dir, "history.json");

    private static readonly byte[] Entropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("AdvancedClipboarder:HistoryStore:v1"));

    private static readonly JsonSerializerOptions Opts = new()
    {
        // Encrypted blob — indentation is pure overhead once it's inside a DPAPI envelope.
        WriteIndented = false,
        // Skip ClipItem's computed accessors (TypeLabel, GroupKey, TypeColor, TemplateKey, Id).
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<ClipItem> Load()
    {
        // One-time migration from pre-encryption builds.
        if (File.Exists(LegacyPath) && !File.Exists(FilePath))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<ClipItem>>(
                                File.ReadAllText(LegacyPath), Opts) ?? new();
                Save(items);                   // writes encrypted .dat
                File.Delete(LegacyPath);       // plaintext copy is no longer allowed on disk
                return items;
            }
            catch
            {
                try { File.Delete(LegacyPath); } catch { }
                return new();
            }
        }

        try
        {
            if (!File.Exists(FilePath)) return new();
            var encrypted = File.ReadAllBytes(FilePath);
            var plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<List<ClipItem>>(plaintext, Opts) ?? new();
        }
        catch
        {
            // Unprotect fails when the Windows profile SID changed (moved machine,
            // user recreated), or the blob is truncated/corrupt. Dropping it keeps
            // the app usable instead of replaying the failure every save.
            try { File.Delete(FilePath); } catch { }
            return new();
        }
    }

    public static void Save(IEnumerable<ClipItem> items)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.SerializeToUtf8Bytes(items, Opts);
            var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

            // Write-then-rename keeps the previous good blob intact if we die mid-write.
            var tmp = FilePath + ".tmp";
            File.WriteAllBytes(tmp, encrypted);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }
        catch { }
    }
}
