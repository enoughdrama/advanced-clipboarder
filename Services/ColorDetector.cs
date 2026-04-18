using System.Text.RegularExpressions;

namespace Clipboarder.Services;

// Whole-string color matchers. Returns true + a normalized form on match.
// Strict by design — we'd rather miss an exotic format than classify prose as a color.
public static class ColorDetector
{
    // #RGB | #RGBA | #RRGGBB | #RRGGBBAA
    private static readonly Regex Hex = new(
        @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled);

    // rgb(r, g, b) / rgba(r, g, b, a) — comma or whitespace-separated, / for alpha (CSS Color 4).
    private static readonly Regex Rgb = new(
        @"^rgba?\s*\(\s*" +
        @"-?\d+(\.\d+)?%?\s*[,\s]\s*" +
        @"-?\d+(\.\d+)?%?\s*[,\s]\s*" +
        @"-?\d+(\.\d+)?%?\s*" +
        @"([,/]\s*\d*\.?\d+%?\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // hsl(h, s%, l%) / hsla(...)
    private static readonly Regex Hsl = new(
        @"^hsla?\s*\(\s*" +
        @"-?\d+(\.\d+)?(deg|rad|turn|grad)?\s*[,\s]\s*" +
        @"\d+(\.\d+)?%\s*[,\s]\s*" +
        @"\d+(\.\d+)?%\s*" +
        @"([,/]\s*\d*\.?\d+%?\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // hwb(h w% b%)
    private static readonly Regex Hwb = new(
        @"^hwb\s*\(\s*" +
        @"-?\d+(\.\d+)?(deg|rad|turn|grad)?\s+" +
        @"\d+(\.\d+)?%\s+" +
        @"\d+(\.\d+)?%\s*" +
        @"(/\s*\d*\.?\d+%?\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // lab(L a b [/ A]) / lch(L C h [/ A]) / oklab(...) / oklch(...)
    private static readonly Regex LabLch = new(
        @"^(ok)?(lab|lch)\s*\(\s*" +
        @"-?\d+(\.\d+)?%?\s+" +
        @"-?\d+(\.\d+)?%?\s+" +
        @"-?\d+(\.\d+)?(deg|rad|turn|grad)?%?\s*" +
        @"(/\s*\d*\.?\d+%?\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // color(<colorspace> r g b [/ A]) — CSS Color 4.
    private static readonly Regex ColorFn = new(
        @"^color\s*\(\s*[\w-]+\s+" +
        @"(-?\d+(\.\d+)?%?\s+){2,3}-?\d+(\.\d+)?%?\s*" +
        @"(/\s*\d*\.?\d+%?\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryDetect(string input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        var t = input.Trim();
        if (t.Length > 80) return false;   // sanity bound: no real color string is this long

        if (Hex.IsMatch(t))     { normalized = t.ToUpperInvariant(); return true; }
        if (Rgb.IsMatch(t))     { normalized = Compact(t); return true; }
        if (Hsl.IsMatch(t))     { normalized = Compact(t); return true; }
        if (Hwb.IsMatch(t))     { normalized = Compact(t); return true; }
        if (LabLch.IsMatch(t))  { normalized = Compact(t); return true; }
        if (ColorFn.IsMatch(t)) { normalized = Compact(t); return true; }

        return false;
    }

    // Collapse inner whitespace runs so pasted values render tidily.
    private static string Compact(string s) => Regex.Replace(s, @"\s+", " ").Trim();
}
