using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clipboarder.Services;

public enum TransformKind
{
    UpperCase,
    LowerCase,
    TitleCase,
    CamelCase,
    PascalCase,
    SnakeCase,
    KebabCase,
    Base64Encode,
    Base64Decode,
    UrlEncode,
    UrlDecode,
    HtmlDecode,
    JsonPrettify,
    JsonMinify,
    UnixToDate,
    DateToUnix,
    Trim,
    NormalizeWhitespace,
    SmartQuotesToAscii,
    DecimalToHex,
    HexToDecimal,
    SortLines,
    ReverseLines,
    DedupLines,
}

// Bitmask of clip kinds a transform should appear under. Not wired to
// ClipType directly so this file stays independent of Clipboarder.Models.
[Flags]
public enum TransformScope
{
    None  = 0,
    Text  = 1 << 0,
    Email = 1 << 1,
    Code  = 1 << 2,
    Link  = 1 << 3,

    // Convenience unions used in the All[] table below.
    TextCode     = Text | Code,
    TextCodeLink = Text | Code | Link,
    Any          = Text | Email | Code | Link,
}

// Pure, side-effect-free text → text conversions applied at paste time.
// Each entry knows its own label + category so the UI can render a menu
// without hardcoding the list twice. Scope filters the menu per clip type
// — e.g. PascalCase doesn't show up for an email address, JSON prettify
// doesn't show up for a URL.
public static class PasteTransforms
{
    // Langs narrows Scope.Code further to specific CodeDetector lang tags
    // (JSON prettify only for Lang="json", HTML decode only for html/xml).
    // null = no narrowing beyond Scope. Ignored for non-Code scopes.
    public readonly record struct Entry(
        TransformKind Kind,
        string Label,
        string Category,
        TransformScope Scope,
        string[]? Langs = null);

    private static readonly string[] LangsJson = { "json" };
    private static readonly string[] LangsMarkup = { "html", "xml" };

    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        new(TransformKind.UpperCase,          "UPPER CASE",             "Case",    TransformScope.TextCode),
        new(TransformKind.LowerCase,          "lower case",             "Case",    TransformScope.Any),
        new(TransformKind.TitleCase,          "Title Case",             "Case",    TransformScope.Text),
        new(TransformKind.CamelCase,          "camelCase",              "Case",    TransformScope.Code),
        new(TransformKind.PascalCase,         "PascalCase",             "Case",    TransformScope.Code),
        new(TransformKind.SnakeCase,          "snake_case",             "Case",    TransformScope.Code),
        new(TransformKind.KebabCase,          "kebab-case",             "Case",    TransformScope.Code),

        new(TransformKind.Base64Encode,       "Base64 encode",          "Encode",  TransformScope.TextCode),
        new(TransformKind.UrlEncode,          "URL encode",             "Encode",  TransformScope.Any),

        new(TransformKind.Base64Decode,       "Base64 decode",          "Decode",  TransformScope.TextCode),
        new(TransformKind.UrlDecode,          "URL decode",             "Decode",  TransformScope.TextCodeLink),
        new(TransformKind.HtmlDecode,         "HTML decode",            "Decode",  TransformScope.Code,     LangsMarkup),

        new(TransformKind.JsonPrettify,       "JSON prettify",          "Format",  TransformScope.Code,     LangsJson),
        new(TransformKind.JsonMinify,         "JSON minify",            "Format",  TransformScope.Code,     LangsJson),

        new(TransformKind.UnixToDate,         "Unix → date",            "Time",    TransformScope.TextCode),
        new(TransformKind.DateToUnix,         "Date → Unix",            "Time",    TransformScope.TextCode),

        new(TransformKind.Trim,               "Trim",                   "Clean",   TransformScope.Any),
        new(TransformKind.NormalizeWhitespace,"Normalize whitespace",   "Clean",   TransformScope.TextCode),
        new(TransformKind.SmartQuotesToAscii, "Smart quotes → ASCII",   "Clean",   TransformScope.TextCode),

        new(TransformKind.DecimalToHex,       "Decimal → hex",          "Convert", TransformScope.Code),
        new(TransformKind.HexToDecimal,       "Hex → decimal",          "Convert", TransformScope.Code),

        new(TransformKind.SortLines,          "Sort lines",             "Lines",   TransformScope.TextCode),
        new(TransformKind.ReverseLines,       "Reverse lines",          "Lines",   TransformScope.TextCode),
        new(TransformKind.DedupLines,         "Dedup lines",            "Lines",   TransformScope.TextCode),
    };

    public static bool LangMatches(Entry entry, string? clipLang)
    {
        if (entry.Langs is null) return true;
        if (string.IsNullOrEmpty(clipLang)) return false;
        foreach (var l in entry.Langs)
            if (string.Equals(l, clipLang, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public static string Apply(TransformKind kind, string input)
    {
        if (input is null) return "";
        try
        {
            return kind switch
            {
                TransformKind.UpperCase           => input.ToUpperInvariant(),
                TransformKind.LowerCase           => input.ToLowerInvariant(),
                TransformKind.TitleCase           => TitleCase(input),
                TransformKind.CamelCase           => CamelCase(input),
                TransformKind.PascalCase          => PascalCase(input),
                TransformKind.SnakeCase           => SnakeCase(input),
                TransformKind.KebabCase           => KebabCase(input),
                TransformKind.Base64Encode        => Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
                TransformKind.Base64Decode        => Base64Decode(input),
                TransformKind.UrlEncode           => Uri.EscapeDataString(input),
                TransformKind.UrlDecode           => Uri.UnescapeDataString(input),
                TransformKind.HtmlDecode          => System.Net.WebUtility.HtmlDecode(input),
                TransformKind.JsonPrettify        => JsonPrettify(input),
                TransformKind.JsonMinify          => JsonMinify(input),
                TransformKind.UnixToDate          => UnixToDate(input),
                TransformKind.DateToUnix          => DateToUnix(input),
                TransformKind.Trim                => input.Trim(),
                TransformKind.NormalizeWhitespace => Regex.Replace(input.Trim(), @"\s+", " "),
                TransformKind.SmartQuotesToAscii  => SmartQuotes(input),
                TransformKind.DecimalToHex        => DecimalToHex(input),
                TransformKind.HexToDecimal        => HexToDecimal(input),
                TransformKind.SortLines           => string.Join("\n", SplitLines(input).OrderBy(x => x, StringComparer.Ordinal)),
                TransformKind.ReverseLines        => string.Join("\n", SplitLines(input).Reverse()),
                TransformKind.DedupLines          => string.Join("\n", SplitLines(input).Distinct()),
                _ => input,
            };
        }
        // Any transform failure (bad JSON, malformed base64, etc.) falls back
        // to the original text — better than pasting an empty string.
        catch { return input; }
    }

    private static string[] SplitLines(string s) =>
        s.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    private static string TitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // Split on non-alphanumerics AND internal camelCase boundaries so arbitrary
    // casing can be normalised into a clean token sequence.
    private static IEnumerable<string> Tokenize(string s)
    {
        var withBoundaries = Regex.Replace(
            s,
            @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
            " ");
        foreach (var part in Regex.Split(withBoundaries, @"[^A-Za-z0-9]+"))
            if (!string.IsNullOrEmpty(part)) yield return part;
    }

    private static string CamelCase(string s)
    {
        var parts = Tokenize(s).ToArray();
        if (parts.Length == 0) return s;
        var sb = new StringBuilder(parts[0].ToLowerInvariant());
        for (int i = 1; i < parts.Length; i++)
        {
            sb.Append(char.ToUpperInvariant(parts[i][0]));
            if (parts[i].Length > 1) sb.Append(parts[i][1..].ToLowerInvariant());
        }
        return sb.ToString();
    }

    private static string PascalCase(string s)
    {
        var sb = new StringBuilder();
        foreach (var p in Tokenize(s))
        {
            sb.Append(char.ToUpperInvariant(p[0]));
            if (p.Length > 1) sb.Append(p[1..].ToLowerInvariant());
        }
        return sb.Length == 0 ? s : sb.ToString();
    }

    private static string SnakeCase(string s) =>
        string.Join("_", Tokenize(s).Select(t => t.ToLowerInvariant()));

    private static string KebabCase(string s) =>
        string.Join("-", Tokenize(s).Select(t => t.ToLowerInvariant()));

    private static string Base64Decode(string input)
    {
        var bytes = Convert.FromBase64String(input.Trim());
        return Encoding.UTF8.GetString(bytes);
    }

    private static string JsonPrettify(string s)
    {
        using var doc = JsonDocument.Parse(s);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            doc.WriteTo(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string JsonMinify(string s)
    {
        using var doc = JsonDocument.Parse(s);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
            doc.WriteTo(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string UnixToDate(string s)
    {
        var t = s.Trim();
        if (!long.TryParse(t, out var n)) return s;
        // 13-digit values are millisecond timestamps (common in JS / Java ecosystems).
        var dt = n >= 1_000_000_000_000L
            ? DateTimeOffset.FromUnixTimeMilliseconds(n)
            : DateTimeOffset.FromUnixTimeSeconds(n);
        return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string DateToUnix(string s)
    {
        if (!DateTime.TryParse(s.Trim(), out var dt)) return s;
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt))
            .ToUnixTimeSeconds()
            .ToString();
    }

    private static string SmartQuotes(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\u2018': case '\u2019': case '\u201A': case '\u201B': case '\u2032':
                    sb.Append('\''); break;
                case '\u201C': case '\u201D': case '\u201E': case '\u201F': case '\u2033':
                    sb.Append('"'); break;
                case '\u2013': case '\u2014': case '\u2212':
                    sb.Append('-'); break;
                case '\u2026':
                    sb.Append("..."); break;
                case '\u00A0':
                    sb.Append(' '); break;
                default:
                    sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string DecimalToHex(string s) =>
        long.TryParse(s.Trim(), out var n) ? "0x" + n.ToString("X") : s;

    private static string HexToDecimal(string s)
    {
        var t = s.Trim().TrimStart('#');
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        return long.TryParse(t, System.Globalization.NumberStyles.HexNumber,
                             System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n.ToString()
            : s;
    }
}
