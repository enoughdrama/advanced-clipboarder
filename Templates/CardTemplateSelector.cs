using System.Windows;
using System.Windows.Controls;
using Clipboarder.Models;

namespace Clipboarder.Templates;

public class CardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? EmailTemplate { get; set; }
    public DataTemplate? CodeTemplate { get; set; }
    public DataTemplate? LinkTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? ColorTemplate { get; set; }
    public DataTemplate? FileTemplate { get; set; }
    public DataTemplate? BigTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not ClipItem c) return base.SelectTemplate(item, container);
        return c.TemplateKey switch
        {
            "Code"  => CodeTemplate,
            "Link"  => LinkTemplate,
            "Image" => ImageTemplate,
            "Color" => ColorTemplate,
            "File"  => FileTemplate,
            "Email" => EmailTemplate,
            "Big"   => BigTemplate,
            _       => TextTemplate,
        };
    }
}
