using Clipboarder.Models;

namespace Clipboarder.Services;

// A single search-box query in parsed form. Any field that's null/empty
// means "don't filter on this axis". Text and Excludes are substring
// matches against Content + Source; the typed filters are strict.
public readonly record struct SearchQuery(
    DateTime? Cutoff,
    string? Source,
    string? Lang,
    ClipType? Type,
    string Text,
    IReadOnlyList<string> Excludes);

// Extends the old >today time-range token with structured filters:
//   source:vscode   — substring match against ClipItem.Source
//   lang:py         — strict match against ClipItem.Lang
//   type:url        — strict match against ClipItem.Type (text/email/code/link/image/color/file)
//   !needle         — exclude any item whose content or source contains "needle"
// Anything else (including unrecognised >tokens) is treated as literal
// text and contributes to the positive substring match.
public static class SearchParser
{
    private static readonly IReadOnlyList<string> EmptyList = Array.Empty<string>();

    public static SearchQuery Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new(null, null, null, null, "", EmptyList);

        DateTime? cutoff = null;
        string? source = null;
        string? lang = null;
        ClipType? type = null;
        List<string>? excludes = null;
        List<string>? textParts = null;

        foreach (var token in query.Split(new[] { ' ', '\t' },
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length == 0) continue;

            if (token[0] == '>')
            {
                var c = ResolveTimeToken(token[1..]);
                if (c is not null) { cutoff = c; continue; }
                // Unrecognised >token — fall through so `>foo` still matches literally.
            }

            if (token[0] == '!' && token.Length > 1)
            {
                (excludes ??= new()).Add(token[1..]);
                continue;
            }

            if (TryStripPrefix(token, "source:", out var srcVal))
            {
                source = srcVal;
                continue;
            }
            if (TryStripPrefix(token, "lang:", out var langVal))
            {
                lang = langVal;
                continue;
            }
            if (TryStripPrefix(token, "type:", out var typeVal))
            {
                if (Enum.TryParse<ClipType>(typeVal, ignoreCase: true, out var t))
                    type = t;
                continue;
            }

            (textParts ??= new()).Add(token);
        }

        return new SearchQuery(
            Cutoff: cutoff,
            Source: source,
            Lang: lang,
            Type: type,
            Text: textParts is null ? "" : string.Join(" ", textParts),
            Excludes: (IReadOnlyList<string>?)excludes ?? EmptyList);
    }

    private static bool TryStripPrefix(string token, string prefix, out string value)
    {
        if (token.Length > prefix.Length
            && token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = token[prefix.Length..];
            return true;
        }
        value = "";
        return false;
    }

    private static DateTime? ResolveTimeToken(string body)
    {
        switch (body.ToLowerInvariant())
        {
            case "hour":      return DateTime.Now.AddHours(-1);
            case "today":     return DateTime.Today;
            case "yesterday": return DateTime.Today.AddDays(-1);
            case "week":      return DateTime.Now.AddDays(-7);
            case "month":     return DateTime.Now.AddDays(-30);
        }
        if (body.Length < 2) return null;
        var suffix = char.ToLowerInvariant(body[^1]);
        if (suffix is not ('m' or 'h' or 'd')) return null;
        if (!int.TryParse(body[..^1], out var n) || n <= 0) return null;
        return suffix switch
        {
            'm' => DateTime.Now.AddMinutes(-n),
            'h' => DateTime.Now.AddHours(-n),
            'd' => DateTime.Now.AddDays(-n),
            _   => null,
        };
    }
}
