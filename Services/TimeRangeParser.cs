namespace Clipboarder.Services;

// Recognises a leading >token in the search query and hands back both a
// cutoff timestamp to filter by and the remaining text to match against.
//
// Supported tokens (case-insensitive):
//   >hour              — last 60 min
//   >today             — since local midnight
//   >yesterday         — since local midnight 2 days ago (includes today)
//   >week              — last 7 days
//   >month             — last 30 days
//   >Nm / >Nh / >Nd    — last N minutes / hours / days (N ≥ 1)
public static class TimeRangeParser
{
    public static (DateTime? Cutoff, string Text) Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return (null, "");
        var q = query.TrimStart();
        if (q.Length < 2 || q[0] != '>') return (null, query.Trim());

        var wsIdx = IndexOfWhitespace(q, 1);
        var token = wsIdx < 0 ? q[1..]     : q[1..wsIdx];
        var rest  = wsIdx < 0 ? ""         : q[(wsIdx + 1)..].TrimStart();

        var cutoff = Resolve(token);
        // If the token wasn't a recognised range, preserve the whole query as
        // literal text — the user might genuinely be searching for ">foo".
        return cutoff is null ? (null, query.Trim()) : (cutoff, rest);
    }

    private static DateTime? Resolve(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "hour":      return DateTime.Now.AddHours(-1);
            case "today":     return DateTime.Today;
            case "yesterday": return DateTime.Today.AddDays(-1);
            case "week":      return DateTime.Now.AddDays(-7);
            case "month":     return DateTime.Now.AddDays(-30);
        }
        // Numeric forms: 15m / 2h / 7d.
        if (token.Length < 2) return null;
        var suffix = char.ToLowerInvariant(token[^1]);
        if (suffix is not ('m' or 'h' or 'd')) return null;
        if (!int.TryParse(token[..^1], out var n) || n <= 0) return null;
        return suffix switch
        {
            'm' => DateTime.Now.AddMinutes(-n),
            'h' => DateTime.Now.AddHours(-n),
            'd' => DateTime.Now.AddDays(-n),
            _ => null,
        };
    }

    private static int IndexOfWhitespace(string s, int start)
    {
        for (int i = start; i < s.Length; i++)
            if (char.IsWhiteSpace(s[i])) return i;
        return -1;
    }
}
