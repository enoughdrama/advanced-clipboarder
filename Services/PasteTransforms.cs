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

// Pure, side-effect-free text → text conversions applied at paste time.
// Each entry knows its own label + category so the UI can render a menu
// without hardcoding the list twice.
public static class PasteTransforms
{
    public readonly record struct Entry(TransformKind Kind, string Label, string Category);

    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        new(TransformKind.UpperCase,          "UPPER CASE",             "Case"),
        new(TransformKind.LowerCase,          "lower case",             "Case"),
        new(TransformKind.TitleCase,          "Title Case",             "Case"),
        new(TransformKind.CamelCase,          "camelCase",              "Case"),
        new(TransformKind.PascalCase,         "PascalCase",             "Case"),
        new(TransformKind.SnakeCase,          "snake_case",             "Case"),
        new(TransformKind.KebabCase,          "kebab-case",             "Case"),

        new(TransformKind.Base64Encode,       "Base64 encode",          "Encode"),
        new(TransformKind.UrlEncode,          "URL encode",             "Encode"),

        new(TransformKind.Base64Decode,       "Base64 decode",          "Decode"),
        new(TransformKind.UrlDecode,          "URL decode",             "Decode"),
        new(TransformKind.HtmlDecode,         "HTML decode",            "Decode"),

        new(TransformKind.JsonPrettify,       "JSON prettify",          "Format"),
        new(TransformKind.JsonMinify,         "JSON minify",            "Format"),

        new(TransformKind.UnixToDate,         "Unix → date",            "Time"),
        new(TransformKind.DateToUnix,         "Date → Unix",            "Time"),

        new(TransformKind.Trim,               "Trim",                   "Clean"),
        new(TransformKind.NormalizeWhitespace,"Normalize whitespace",   "Clean"),
        new(TransformKind.SmartQuotesToAscii, "Smart quotes → ASCII",   "Clean"),

        new(TransformKind.DecimalToHex,       "Decimal → hex",          "Convert"),
        new(TransformKind.HexToDecimal,       "Hex → decimal",          "Convert"),

        new(TransformKind.SortLines,          "Sort lines",             "Lines"),
        new(TransformKind.ReverseLines,       "Reverse lines",          "Lines"),
        new(TransformKind.DedupLines,         "Dedup lines",            "Lines"),
    };

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
