using System.Globalization;
using System.Text.RegularExpressions;

namespace Clipboarder.Services;

public enum ColorFormat { Hex, Rgb, Hsl, Oklch }

// Color-format converter. Parses any of the four target formats into a unified
// linear-RGB-A record, then formats on demand. OKLab conversion follows
// Björn Ottosson's reference implementation (https://bottosson.github.io/posts/oklab/).
public static class ColorFormatSwap
{
    public readonly record struct Entry(ColorFormat Format, string Label);

    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        new(ColorFormat.Hex,   "HEX"),
        new(ColorFormat.Rgb,   "RGB"),
        new(ColorFormat.Hsl,   "HSL"),
        new(ColorFormat.Oklch, "OKLCH"),
    };

    public static ColorFormat? Detect(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var t = input.TrimStart();
        if (t.StartsWith('#')) return ColorFormat.Hex;
        if (t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return ColorFormat.Rgb;
        if (t.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) return ColorFormat.Hsl;
        if (t.StartsWith("oklch", StringComparison.OrdinalIgnoreCase)) return ColorFormat.Oklch;
        return null;
    }

    public static string? Convert(string input, ColorFormat target)
    {
        if (!TryParse(input, out var c)) return null;
        return target switch
        {
            ColorFormat.Hex   => FormatHex(c),
            ColorFormat.Rgb   => FormatRgb(c),
            ColorFormat.Hsl   => FormatHsl(c),
            ColorFormat.Oklch => FormatOklch(c),
            _ => null,
        };
    }

    // R/G/B/A stored in the 0..1 sRGB gamma-encoded space — simplest to round-trip.
    private readonly record struct Rgba(double R, double G, double B, double A);

    private static bool TryParse(string s, out Rgba c)
    {
        var t = s.Trim();
        if (TryParseHex(t, out c))   return true;
        if (TryParseRgb(t, out c))   return true;
        if (TryParseHsl(t, out c))   return true;
        if (TryParseOklch(t, out c)) return true;
        c = default;
        return false;
    }

    private static bool TryParseHex(string s, out Rgba c)
    {
        c = default;
        if (!s.StartsWith('#')) return false;
        var h = s[1..];
        if (h.Length is not (3 or 4 or 6 or 8)) return false;
        foreach (var ch in h) if (!IsHex(ch)) return false;

        // Short hex (#RGB / #RGBA) expands each nibble to a full byte.
        string full;
        if (h.Length is 3 or 4)
        {
            var sb = new System.Text.StringBuilder(h.Length * 2);
            foreach (var ch in h) { sb.Append(ch); sb.Append(ch); }
            full = sb.ToString();
        }
        else full = h;

        double r = ParseHexByte(full, 0) / 255.0;
        double g = ParseHexByte(full, 2) / 255.0;
        double b = ParseHexByte(full, 4) / 255.0;
        double a = full.Length == 8 ? ParseHexByte(full, 6) / 255.0 : 1.0;
        c = new(r, g, b, a);
        return true;
    }

    private static bool IsHex(char ch) => ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    private static int ParseHexByte(string s, int i) =>
        int.Parse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static readonly Regex RgbRe = new(
        @"^rgba?\s*\(\s*(?<r>-?\d+(?:\.\d+)?%?)\s*[,\s]\s*(?<g>-?\d+(?:\.\d+)?%?)\s*[,\s]\s*(?<b>-?\d+(?:\.\d+)?%?)\s*([,/]\s*(?<a>-?\d*\.?\d+%?)\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool TryParseRgb(string s, out Rgba c)
    {
        c = default;
        var m = RgbRe.Match(s);
        if (!m.Success) return false;
        double r = ParseByte(m.Groups["r"].Value);
        double g = ParseByte(m.Groups["g"].Value);
        double b = ParseByte(m.Groups["b"].Value);
        double a = m.Groups["a"].Success ? ParseAlpha(m.Groups["a"].Value) : 1.0;
        c = new(Clamp01(r), Clamp01(g), Clamp01(b), Clamp01(a));
        return true;
    }

    private static double ParseByte(string v)
    {
        if (v.EndsWith('%'))
            return double.Parse(v[..^1], CultureInfo.InvariantCulture) / 100.0;
        return double.Parse(v, CultureInfo.InvariantCulture) / 255.0;
    }

    private static double ParseAlpha(string v)
    {
        if (v.EndsWith('%'))
            return double.Parse(v[..^1], CultureInfo.InvariantCulture) / 100.0;
        return double.Parse(v, CultureInfo.InvariantCulture);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    private static readonly Regex HslRe = new(
        @"^hsla?\s*\(\s*(?<h>-?\d+(?:\.\d+)?)(deg|rad|turn|grad)?\s*[,\s]\s*(?<s>\d+(?:\.\d+)?)%\s*[,\s]\s*(?<l>\d+(?:\.\d+)?)%\s*([,/]\s*(?<a>-?\d*\.?\d+%?)\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool TryParseHsl(string s, out Rgba c)
    {
        c = default;
        var m = HslRe.Match(s);
        if (!m.Success) return false;
        double h   = double.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        double sat = double.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture) / 100.0;
        double l   = double.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture) / 100.0;
        double a   = m.Groups["a"].Success ? ParseAlpha(m.Groups["a"].Value) : 1.0;
        HslToRgb(h, sat, l, out var r, out var g, out var b);
        c = new(Clamp01(r), Clamp01(g), Clamp01(b), Clamp01(a));
        return true;
    }

    private static readonly Regex OklchRe = new(
        @"^oklch\s*\(\s*(?<l>-?\d*\.?\d+%?)\s+(?<c>-?\d*\.?\d+)\s+(?<h>-?\d*\.?\d+)(deg|rad|turn|grad)?\s*(/\s*(?<a>-?\d*\.?\d+%?)\s*)?\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool TryParseOklch(string s, out Rgba c)
    {
        c = default;
        var m = OklchRe.Match(s);
        if (!m.Success) return false;
        double l = m.Groups["l"].Value.EndsWith('%')
            ? double.Parse(m.Groups["l"].Value[..^1], CultureInfo.InvariantCulture) / 100.0
            : double.Parse(m.Groups["l"].Value, CultureInfo.InvariantCulture);
        double chroma = double.Parse(m.Groups["c"].Value, CultureInfo.InvariantCulture);
        double h      = double.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        double a      = m.Groups["a"].Success ? ParseAlpha(m.Groups["a"].Value) : 1.0;
        OklchToRgb(l, chroma, h, out var r, out var g, out var b);
        c = new(Clamp01(r), Clamp01(g), Clamp01(b), Clamp01(a));
        return true;
    }

    private static string FormatHex(Rgba c)
    {
        int r = (int)Math.Round(c.R * 255);
        int g = (int)Math.Round(c.G * 255);
        int b = (int)Math.Round(c.B * 255);
        if (c.A < 0.999)
        {
            int a = (int)Math.Round(c.A * 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string FormatRgb(Rgba c)
    {
        int r = (int)Math.Round(c.R * 255);
        int g = (int)Math.Round(c.G * 255);
        int b = (int)Math.Round(c.B * 255);
        return c.A < 0.999
            ? FormattableString.Invariant($"rgba({r}, {g}, {b}, {c.A:0.##})")
            : FormattableString.Invariant($"rgb({r}, {g}, {b})");
    }

    private static string FormatHsl(Rgba c)
    {
        RgbToHsl(c.R, c.G, c.B, out var h, out var s, out var l);
        int hi = (int)Math.Round(h);
        int si = (int)Math.Round(s * 100);
        int li = (int)Math.Round(l * 100);
        return c.A < 0.999
            ? FormattableString.Invariant($"hsla({hi}, {si}%, {li}%, {c.A:0.##})")
            : FormattableString.Invariant($"hsl({hi}, {si}%, {li}%)");
    }

    private static string FormatOklch(Rgba c)
    {
        RgbToOklch(c.R, c.G, c.B, out var l, out var chroma, out var h);
        return c.A < 0.999
            ? FormattableString.Invariant($"oklch({l:0.####} {chroma:0.####} {h:0.##} / {c.A:0.##})")
            : FormattableString.Invariant($"oklch({l:0.####} {chroma:0.####} {h:0.##})");
    }

    private static void HslToRgb(double h, double s, double l,
                                 out double r, out double g, out double b)
    {
        h = ((h % 360) + 360) % 360;
        if (s == 0) { r = g = b = l; return; }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double hh = h / 360;
        r = HueToRgb(p, q, hh + 1.0 / 3);
        g = HueToRgb(p, q, hh);
        b = HueToRgb(p, q, hh - 1.0 / 3);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private static void RgbToHsl(double r, double g, double b,
                                 out double h, out double s, out double l)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2;
        if (max == min) { h = 0; s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if      (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else               h = (r - g) / d + 4;
        h *= 60;
    }

    private static void RgbToOklch(double r, double g, double b,
                                   out double l, out double c, out double h)
    {
        double lr = SrgbToLinear(r), lg = SrgbToLinear(g), lb = SrgbToLinear(b);
        double ll = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb;
        double mm = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb;
        double ss = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb;
        ll = Math.Cbrt(ll); mm = Math.Cbrt(mm); ss = Math.Cbrt(ss);

        double okL = 0.2104542553 * ll + 0.7936177850 * mm - 0.0040720468 * ss;
        double okA = 1.9779984951 * ll - 2.4285922050 * mm + 0.4505937099 * ss;
        double okB = 0.0259040371 * ll + 0.7827717662 * mm - 0.8086757660 * ss;

        l = okL;
        c = Math.Sqrt(okA * okA + okB * okB);
        h = Math.Atan2(okB, okA) * 180 / Math.PI;
        if (h < 0) h += 360;
    }

    private static void OklchToRgb(double l, double c, double h,
                                   out double r, out double g, out double b)
    {
        double okA = c * Math.Cos(h * Math.PI / 180);
        double okB = c * Math.Sin(h * Math.PI / 180);

        double ll = l + 0.3963377774 * okA + 0.2158037573 * okB;
        double mm = l - 0.1055613458 * okA - 0.0638541728 * okB;
        double ss = l - 0.0894841775 * okA - 1.2914855480 * okB;
        ll = ll * ll * ll; mm = mm * mm * mm; ss = ss * ss * ss;

        double lr =  4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss;
        double lg = -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss;
        double lb = -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss;

        r = LinearToSrgb(lr);
        g = LinearToSrgb(lg);
        b = LinearToSrgb(lb);
    }

    private static double SrgbToLinear(double v) =>
        v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);

    private static double LinearToSrgb(double v) =>
        v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
}
