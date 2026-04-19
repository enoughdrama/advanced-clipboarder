using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clipboarder.Services;

public sealed record UpdateInfo(
    Version Latest,
    string TagName,
    string Title,
    string DownloadUrl,
    string ReleaseUrl);

// GitHub release check + background download + silent install hand-off.
//
// Expected release shape on https://github.com/enoughdrama/advanced-clipboarder/releases:
//   - tag name follows "vMAJOR.MINOR.PATCH" (the leading "v" is tolerated)
//   - contains a setup asset whose filename matches the SetupAssetPattern below
// If either is missing, CheckAsync returns null and the caller shows nothing.
public static class UpdateService
{
    private const string Owner              = "enoughdrama";
    private const string Repo               = "advanced-clipboarder";
    private const string SetupAssetPrefix   = "AdvancedClipboarder-";
    private const string SetupAssetSuffix   = "-win-x64-setup.exe";

    private static readonly HttpClient _http = BuildHttpClient();

    private static HttpClient BuildHttpClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var h = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var v = CurrentVersion()?.ToString(3) ?? "0.0.0";
        h.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Clipboarder", v));
        h.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return h;
    }

    public static Version? CurrentVersion()
    {
        // <Version> in Clipboarder.csproj stamps AssemblyVersion. GetName().Version
        // returns it cleanly (AssemblyInformationalVersion can carry "+<gitsha>" which
        // Version.TryParse rejects).
        return Assembly.GetExecutingAssembly().GetName().Version;
    }

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            await using var body = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var rel = await JsonSerializer.DeserializeAsync<GhRelease>(body, cancellationToken: ct).ConfigureAwait(false);
            if (rel is null || string.IsNullOrWhiteSpace(rel.TagName)) return null;

            var latest = ParseVersion(rel.TagName);
            var current = CurrentVersion();
            if (latest is null || current is null) return null;
            if (latest <= current) return null;

            var asset = rel.Assets?.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Name)
                && a.Name.StartsWith(SetupAssetPrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(SetupAssetSuffix, StringComparison.OrdinalIgnoreCase));
            if (asset?.DownloadUrl is null) return null;

            return new UpdateInfo(
                latest,
                rel.TagName,
                rel.Name ?? rel.TagName,
                asset.DownloadUrl,
                rel.HtmlUrl ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> DownloadAsync(
        string url,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        string? tmp = null;
        try
        {
            tmp = Path.Combine(Path.GetTempPath(), $"clipboarder-update-{Guid.NewGuid():N}.exe");
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }

            return tmp;
        }
        catch
        {
            if (tmp is not null) try { File.Delete(tmp); } catch { }
            return null;
        }
    }

    // Fires the installer with silent + close-and-restart flags; our own process stays alive
    // just long enough to hand over — Inno Setup will terminate it via CloseApplications=force.
    public static bool LaunchInstaller(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NOCANCEL",
                UseShellExecute = true,
            };
            return Process.Start(psi) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static Version? ParseVersion(string tag)
    {
        // Accepts "v0.1.0", "0.1.0", "v0.1.0-beta" (beta suffix stripped).
        var s = tag.TrimStart('v', 'V');
        var dash = s.IndexOf('-');
        if (dash > 0) s = s[..dash];
        return Version.TryParse(s, out var v) ? v : null;
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")]     public string? Name    { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("assets")]   public List<GhAsset>? Assets { get; set; }
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")]                 public string? Name        { get; set; }
        [JsonPropertyName("browser_download_url")] public string? DownloadUrl { get; set; }
    }
}
