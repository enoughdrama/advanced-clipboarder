using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Clipboarder.Services;

namespace Clipboarder.Models;

public enum ClipType
{
    Text,
    Email,
    Code,
    Link,
    Image,
    Color,
    File,
}

public class ClipItem : INotifyPropertyChanged
{
    private static int _next = 1;
    public int Id { get; } = System.Threading.Interlocked.Increment(ref _next);

    public ClipType Type { get; init; }
    public string Content { get; init; } = "";
    public string Source { get; init; } = "";
    public string? Lang { get; init; }
    public string? Tag { get; init; }

    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? FileSize { get; init; }

    public string[]? Palette { get; init; }

    private DateTime _timestamp = DateTime.Now;
    public DateTime Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnChanged(); OnChanged(nameof(TimeAgo)); OnChanged(nameof(GroupKey)); }
    }

    private bool _pinned;
    public bool Pinned
    {
        get => _pinned;
        set { if (_pinned != value) { _pinned = value; OnChanged(); OnChanged(nameof(GroupKey)); } }
    }

    private bool _isFresh;
    [JsonIgnore]
    public bool IsFresh
    {
        get => _isFresh;
        set { if (_isFresh != value) { _isFresh = value; OnChanged(); } }
    }

    public string TypeLabel => Type switch
    {
        ClipType.Text  => "TXT",
        ClipType.Email => "@",
        ClipType.Code  => (Lang ?? "code").ToUpperInvariant(),
        ClipType.Link  => "URL",
        ClipType.Image => "IMG",
        ClipType.Color => "HEX",
        ClipType.File  => "FILE",
        _ => "TXT",
    };

    public string GroupKey
    {
        get
        {
            if (Pinned) return "Pinned";
            var diff = DateTime.Now - Timestamp;
            if (diff.TotalHours < 1) return "Last hour";
            if (diff.TotalDays < 1) return "Today";
            if (diff.TotalDays < 2) return "Yesterday";
            return "Earlier";
        }
    }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.Now - Timestamp;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }

    public Brush TypeColor
    {
        get
        {
            var key = Type switch
            {
                ClipType.Text  => "TypeTextBrush",
                ClipType.Email => "TypeEmailBrush",
                ClipType.Code  => "TypeCodeBrush",
                ClipType.Link  => "TypeLinkBrush",
                ClipType.Image => "TypeImgBrush",
                ClipType.Color => "TypeColorBrush",
                ClipType.File  => "TypeFileBrush",
                _ => "TypeTextBrush",
            };
            return (Brush)System.Windows.Application.Current.Resources[key];
        }
    }

    // Drives CardTemplateSelector — changing these keys requires updating the selector.
    public string TemplateKey
    {
        get
        {
            if (Type == ClipType.Code)  return "Code";
            if (Type == ClipType.Link)  return "Link";
            if (Type == ClipType.Color) return "Color";
            if (Type == ClipType.Image) return "Image";
            if (Type == ClipType.File)  return "File";
            if (Type == ClipType.Email) return "Email";
            if (Tag == "2FA" || (Content.Length is >= 4 and <= 8 && Content.All(char.IsDigit))) return "Big";
            return "Text";
        }
    }

    // A text-ish clip whose content has {date}/{clipboard}/{input:…}/etc. tokens.
    // Pasting such a clip goes through TemplateEngine + PromptDialog instead
    // of the direct clipboard-set path. Computed, so editing Content (which
    // is init-only today) is unnecessary.
    [JsonIgnore]
    public bool IsTemplate =>
        Type is ClipType.Text or ClipType.Code or ClipType.Email
        && TemplateEngine.IsTemplate(Content);

    // Drives the Transform button's visibility on each card. Image + File
    // clips don't have any meaningful paste-time conversion, so hide the
    // button entirely rather than open an empty menu.
    [JsonIgnore]
    public bool HasTransforms =>
        Type is ClipType.Text or ClipType.Code or ClipType.Email
           or ClipType.Link or ClipType.Color;

    public void Refresh()
    {
        OnChanged(nameof(TimeAgo));
        OnChanged(nameof(GroupKey));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}
