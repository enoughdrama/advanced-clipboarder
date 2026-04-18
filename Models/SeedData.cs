namespace Clipboarder.Models;

public static class SeedData
{
    public static List<ClipItem> Build()
    {
        var now = DateTime.Now;
        return new List<ClipItem>
        {
            new()
            {
                Type = ClipType.Code, Lang = "ts", Source = "VS Code", Pinned = true,
                Timestamp = now.AddMinutes(-2),
                Content = "const useDebounce = (v, ms = 200) => {\n  const [x, setX] = useState(v);\n  useEffect(() => { const t = setTimeout(() => setX(v), ms); return () => clearTimeout(t); }, [v, ms]);\n  return x;\n};"
            },
            new()
            {
                Type = ClipType.Link, Source = "Firefox",
                Timestamp = now.AddMinutes(-7),
                Content = "https://datatracker.ietf.org/doc/html/rfc9110#name-http-semantics"
            },
            new()
            {
                Type = ClipType.Color, Source = "Figma", Pinned = true,
                Timestamp = now.AddMinutes(-14),
                Content = "#7C5CFF",
                Palette = new[] { "#7C5CFF", "#5CE1E6", "#FFB86A", "#F06292" }
            },
            new()
            {
                Type = ClipType.Text, Source = "Mail",
                Timestamp = now.AddMinutes(-28),
                Content = "Reminder: the quarterly review is moved to Thursday at 14:00 CET. Please confirm attendance in the calendar invite and bring the updated metrics deck."
            },
            new()
            {
                Type = ClipType.Email, Source = "Contacts",
                Timestamp = now.AddMinutes(-42),
                Content = "lena.ortiz@atlaslabs.io"
            },
            new()
            {
                Type = ClipType.Image, Source = "Screenshot",
                Timestamp = now.AddHours(-1),
                Content = "hero-illustration-v3.png",
                Width = 1440, Height = 960, FileSize = "284 KB"
            },
            new()
            {
                Type = ClipType.Text, Source = "Messages", Tag = "2FA",
                Timestamp = now.AddHours(-2),
                Content = "092088"
            },
            new()
            {
                Type = ClipType.Code, Lang = "sh", Source = "Terminal",
                Timestamp = now.AddHours(-3),
                Content = "ssh -i ~/.ssh/atlas_rsa deploy@edge-03.atlas.internal -p 2022"
            },
            new()
            {
                Type = ClipType.Text, Source = "Notes",
                Timestamp = now.AddHours(-5),
                Content = "\u201CSimplicity is the ultimate sophistication, but it is the hardest thing to reach \u2014 not because it hides, but because we insist on carrying too much.\u201D"
            },
            new()
            {
                Type = ClipType.File, Source = "Finder",
                Timestamp = now.AddHours(-8),
                Content = "Q3-roadmap-final.pdf", FileSize = "1.4 MB"
            },
            new()
            {
                Type = ClipType.Link, Source = "Chrome",
                Timestamp = now.AddDays(-1),
                Content = "https://github.com/atlaslabs/kernel/pull/4821"
            },
            new()
            {
                Type = ClipType.Text, Source = "Maps",
                Timestamp = now.AddDays(-1).AddHours(-2),
                Content = "Atlas Labs \u00B7 221B Baker Street \u00B7 London \u00B7 NW1 6XE \u00B7 United Kingdom"
            },
            new()
            {
                Type = ClipType.Code, Lang = "json", Source = "Postman",
                Timestamp = now.AddDays(-2),
                Content = "{\n  \"id\": \"usr_01HZ8\",\n  \"email\": \"lena@atlas.io\",\n  \"role\": \"admin\",\n  \"scopes\": [\"read\",\"write\",\"deploy\"]\n}"
            },
            new()
            {
                Type = ClipType.Image, Source = "Photos",
                Timestamp = now.AddDays(-3),
                Content = "team-offsite-lisbon.jpg",
                Width = 3024, Height = 4032, FileSize = "3.2 MB"
            },
        };
    }
}
