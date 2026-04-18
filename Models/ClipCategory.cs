namespace Clipboarder.Models;

public record ClipCategory(string Id, string Label, string IconKey, Func<ClipItem, bool> Match)
{
    public static readonly IReadOnlyList<ClipCategory> All = new List<ClipCategory>
    {
        new("all",    "All",    "IconAll",   _ => true),
        new("pinned", "Pinned", "IconPin",   x => x.Pinned),
        new("text",   "Text",   "IconText",  x => x.Type is ClipType.Text or ClipType.Email),
        new("code",   "Code",   "IconCode",  x => x.Type == ClipType.Code),
        new("link",   "Links",  "IconLink",  x => x.Type == ClipType.Link),
        new("img",    "Images", "IconImage", x => x.Type == ClipType.Image),
        new("color",  "Colors", "IconColor", x => x.Type == ClipType.Color),
        new("file",   "Files",  "IconFile",  x => x.Type == ClipType.File),
    };
}
