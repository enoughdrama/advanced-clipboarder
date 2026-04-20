using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Clipboarder.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null || value is string s && string.IsNullOrEmpty(s);
        return (isNull ^ Invert) ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(c);
            }
            catch { }
        }
        return Brushes.Transparent;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Decodes a file path into a BitmapImage for Image.Source bindings.
// CacheOption=OnLoad reads the file fully at decode so we don't hold a file handle.
public class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        if (!System.IO.File.Exists(path)) return null;
        try
        {
            var img = new System.Windows.Media.Imaging.BitmapImage();
            img.BeginInit();
            img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            img.DecodePixelHeight = 160;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class UpperCaseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string)?.ToUpperInvariant() ?? "";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Like PathToImageConverter but decodes at ~400px for the hover preview.
// Keeps the card-sized version at 160px so list scrolling stays fast.
public class PathToBigImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        if (!System.IO.File.Exists(path)) return null;
        try
        {
            var img = new System.Windows.Media.Imaging.BitmapImage();
            img.BeginInit();
            img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            img.DecodePixelHeight = 400;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Formats WCAG 2.1 contrast ratios against pure white and pure black for a
// given hex / rgb / hsl string. Returns "12.5 : 1 on white · 1.7 : 1 on black"
// or empty on parse failure.
public class ContrastRatioConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return "";
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(s.Trim());
            var L = Luminance(c.R, c.G, c.B);
            var onWhite = (1.0 + 0.05) / (L + 0.05);
            var onBlack = (L + 0.05) / (0.0 + 0.05);
            return string.Format(CultureInfo.InvariantCulture,
                "{0:0.0}:1 on white  ·  {1:0.0}:1 on black", onWhite, onBlack);
        }
        catch { return ""; }
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double Luminance(byte r, byte g, byte b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(byte v)
    {
        var s = v / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}

public class UrlHostConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return "";
        if (!Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri)) return s;
        return uri.Scheme + "://" + uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port);
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class UrlPathConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return "";
        if (!Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri)) return "";
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var frag = string.IsNullOrEmpty(uri.Fragment) ? "" : uri.Fragment;
        return path + frag;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class UrlQueryPartsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return Array.Empty<object>();
        if (!Uri.TryCreate(s.Trim(), UriKind.Absolute, out var uri)) return Array.Empty<object>();
        if (string.IsNullOrEmpty(uri.Query)) return Array.Empty<object>();

        var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
        foreach (var pair in uri.Query.TrimStart('?').Split('&'))
        {
            if (pair.Length == 0) continue;
            var eq = pair.IndexOf('=');
            var key = eq >= 0 ? pair[..eq] : pair;
            var val = eq >= 0 ? pair[(eq + 1)..] : "";
            try
            {
                list.Add(new(Uri.UnescapeDataString(key), Uri.UnescapeDataString(val)));
            }
            catch
            {
                list.Add(new(key, val));
            }
        }
        return list;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Visible only when the clip content looks like Base64 AND decodes cleanly.
// Wraps the preview converter's success-check in a single step so XAML can
// toggle a whole preview section.
public class Base64VisibilityConverter : IValueConverter
{
    private static readonly Base64DecodePreviewConverter Decoder = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var decoded = Decoder.Convert(value, typeof(string), null, culture) as string;
        return string.IsNullOrEmpty(decoded) ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Decodes a Base64-looking string when the clip content passes a few cheap
// heuristics (length ≥ 16, proper alphabet, successful decode, mostly
// printable result). Returns the decoded preview or an empty string so
// the hover template can hide the section.
public class Base64DecodePreviewConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return "";
        var trimmed = s.Trim();
        if (trimmed.Length < 16 || trimmed.Length > 10_000) return "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9+/=]+$")) return "";
        if (trimmed.Length % 4 != 0) return "";

        try
        {
            var bytes = System.Convert.FromBase64String(trimmed);
            if (bytes.Length == 0) return "";
            var txt = System.Text.Encoding.UTF8.GetString(bytes);

            int printable = 0;
            foreach (var ch in txt)
            {
                if (ch == '\n' || ch == '\r' || ch == '\t' || (ch >= 0x20 && ch < 0x7F))
                    printable++;
            }
            if (printable < txt.Length * 0.85) return "";
            return txt.Length > 400 ? txt[..400] + "…" : txt;
        }
        catch { return ""; }
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ImageCaptionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 4) return "";
        var raw = values[0] as string ?? "";
        // Strip directory so captured-clipboard blobs don't show their full AppData path.
        var name = string.IsNullOrEmpty(raw) ? "" : System.IO.Path.GetFileName(raw);
        var w = values[1];
        var h = values[2];
        var size = values[3] as string ?? "";
        var dims = (w is int wi && h is int hi) ? $"{wi}\u00D7{hi}" : "";
        var parts = new[] { dims, size, name }.Where(s => !string.IsNullOrEmpty(s));
        return string.Join(" \u00B7 ", parts);
    }
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
