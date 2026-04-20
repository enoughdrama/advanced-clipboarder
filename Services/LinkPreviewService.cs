using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;

namespace Clipboarder.Services;

public static class LinkPreviewService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
    };

    static LinkPreviewService()
    {
        Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,*/*;q=0.8");
    }

    // og:title — attribute order A: property before content
    private static readonly Regex OgTitleA = new(
        @"<meta\b[^>]*\bproperty=[""']og:title[""'][^>]*\bcontent=[""'](?<v>[^""'<]{1,250})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // og:title — attribute order B: content before property
    private static readonly Regex OgTitleB = new(
        @"<meta\b[^>]*\bcontent=[""'](?<v>[^""'<]{1,250})[""'][^>]*\bproperty=[""']og:title[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PageTitle = new(
        @"<title\b[^>]*>(?<v>[^<]{1,250})</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static void FetchAsync(LinkPreview preview, string url)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        _ = Task.Run(async () =>
        {
            string? title = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
                if (ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                    var buf = new byte[65536];
                    var n = await stream.ReadAsync(buf, 0, buf.Length, cts.Token);
                    var html = System.Text.Encoding.UTF8.GetString(buf, 0, n);
                    title = ExtractTitle(html);
                }
            }
            catch { }

            _ = dispatcher.BeginInvoke(() =>
            {
                preview.Title = title;
                preview.IsLoading = false;
            });
        });
    }

    private static string? ExtractTitle(string html)
    {
        var m = OgTitleA.Match(html);
        if (!m.Success) m = OgTitleB.Match(html);
        if (!m.Success) m = PageTitle.Match(html);
        if (!m.Success) return null;

        var t = HtmlDecode(m.Groups["v"].Value.Trim());
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t.Length > 0 ? t : null;
    }

    private static string HtmlDecode(string s) => System.Net.WebUtility.HtmlDecode(s);
}
