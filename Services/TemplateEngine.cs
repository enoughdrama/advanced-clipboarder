using System.Text.RegularExpressions;

namespace Clipboarder.Services;

// Renders clip content with {token} substitutions at paste time. A pinned
// text clip containing any known token becomes a "template": when pasted,
// tokens are replaced with live values and any {input:Label} tokens prompt
// the user for a value first.
//
// Supported tokens (case-insensitive):
//   {date}          — today (yyyy-MM-dd)   — {date:MMM d, yyyy} for custom format
//   {time}          — current time (HH:mm)  — {time:h:mm tt} for custom format
//   {datetime}      — local now (yyyy-MM-dd HH:mm:ss) — {datetime:<fmt>}
//   {utc}           — UTC now               — {utc:<fmt>}
//   {iso}           — ISO 8601 local timestamp
//   {uuid} / {guid} — fresh GUID
//   {clipboard}     — current system clipboard text
//   {input:Label}   — user is prompted for "Label"; identical labels collapse
public static class TemplateEngine
{
    private static readonly Regex TokenRe = new(
        @"\{(\w+)(?::([^}]+))?\}",
        RegexOptions.Compiled);

    public static bool IsTemplate(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        foreach (Match m in TokenRe.Matches(content))
            if (IsKnown(m.Groups[1].Value)) return true;
        return false;
    }

    // Stable order + deduplicated labels for {input:Foo} prompts.
    public static IReadOnlyList<string> CollectInputLabels(string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<string>();
        var list = new List<string>();
        foreach (Match m in TokenRe.Matches(content))
        {
            if (!string.Equals(m.Groups[1].Value, "input", StringComparison.OrdinalIgnoreCase))
                continue;
            var label = m.Groups[2].Success ? m.Groups[2].Value.Trim() : "Value";
            if (list.All(x => !string.Equals(x, label, StringComparison.Ordinal)))
                list.Add(label);
        }
        return list;
    }

    public static string Render(string content, IDictionary<string, string>? inputs = null)
    {
        if (string.IsNullOrEmpty(content)) return content;
        return TokenRe.Replace(content, m =>
        {
            var key = m.Groups[1].Value.ToLowerInvariant();
            var param = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;

            return key switch
            {
                "date"     => DateTime.Now.ToString(param ?? "yyyy-MM-dd"),
                "time"     => DateTime.Now.ToString(param ?? "HH:mm"),
                "datetime" => DateTime.Now.ToString(param ?? "yyyy-MM-dd HH:mm:ss"),
                "utc"      => DateTime.UtcNow.ToString(param ?? "yyyy-MM-dd HH:mm:ssZ"),
                "iso"      => DateTime.Now.ToString("o"),
                "uuid" or "guid" => Guid.NewGuid().ToString(),
                "clipboard" => TryReadClipboard(),
                "input" when inputs is not null && param is not null
                           && inputs.TryGetValue(param, out var v) => v,
                "input" when inputs is not null && param is null
                           && inputs.TryGetValue("Value", out var v2) => v2,
                // Unknown tokens are left verbatim so typos show up as
                // literal {foo} rather than silently disappearing.
                _ => m.Value,
            };
        });
    }

    private static bool IsKnown(string name) => name.ToLowerInvariant() switch
    {
        "date" or "time" or "datetime" or "utc" or "iso" or "uuid" or "guid"
            or "clipboard" or "input" => true,
        _ => false,
    };

    private static string TryReadClipboard()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText()
                ? System.Windows.Clipboard.GetText() ?? ""
                : "";
        }
        catch { return ""; }
    }
}
