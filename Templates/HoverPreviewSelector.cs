using System.Windows;
using System.Windows.Controls;
using Clipboarder.Models;

namespace Clipboarder.Templates;

public class HoverPreviewSelector : DataTemplateSelector
{
    public DataTemplate? ColorTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? LinkTemplate { get; set; }
    public DataTemplate? JsonTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not ClipItem c) return DefaultTemplate;
        return c.Type switch
        {
            ClipType.Color => ColorTemplate,
            ClipType.Image => ImageTemplate,
            ClipType.Link  => LinkTemplate,
            ClipType.Code when string.Equals(c.Lang, "json", StringComparison.OrdinalIgnoreCase)
                           => JsonTemplate,
            _              => DefaultTemplate,
        };
    }
}
