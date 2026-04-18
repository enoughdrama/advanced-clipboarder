using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DColor = System.Drawing.Color;
using DBrush = System.Drawing.Brush;

namespace Clipboarder.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;

    public event Action? ShowRequested;
    public event Action? QuitRequested;
    public event Action? ClearRequested;
    public event Action<bool>? PauseToggled;

    public TrayService()
    {
        _icon = new NotifyIcon
        {
            Icon = BuildIcon(),
            Visible = true,
            Text = "Clipboarder",
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowRequested?.Invoke();
        };

        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = DColor.FromArgb(30, 28, 38),
            ForeColor = DColor.FromArgb(244, 241, 234),
            ShowImageMargin = false,
        };

        var showItem = new ToolStripMenuItem("Show Clipboarder") { Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        showItem.Click += (_, _) => ShowRequested?.Invoke();

        _pauseItem = new ToolStripMenuItem("Pause capture") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => PauseToggled?.Invoke(_pauseItem.Checked);

        var clearItem = new ToolStripMenuItem("Clear history");
        clearItem.Click += (_, _) => ClearRequested?.Invoke();

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(clearItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _icon.ContextMenuStrip = menu;
    }

    public void SetPaused(bool paused)
    {
        if (_pauseItem.Checked != paused) _pauseItem.Checked = paused;
    }

    private static Icon BuildIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(DColor.Transparent);
            using DBrush bg = new SolidBrush(DColor.FromArgb(30, 28, 38));
            g.FillRectangle(bg, 0, 0, 16, 16);
            using DBrush accent = new SolidBrush(DColor.FromArgb(158, 123, 240));
            g.FillRectangle(accent, 3, 2, 10, 3);
            using DBrush line = new SolidBrush(DColor.FromArgb(199, 194, 184));
            g.FillRectangle(line, 3, 7, 10, 1);
            g.FillRectangle(line, 3, 10, 10, 1);
            g.FillRectangle(line, 3, 13, 6, 1);
        }
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = DColor.FromArgb(244, 241, 234);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(DColor.FromArgb(40, 255, 255, 255));
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 6, y);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override DColor MenuItemSelected => DColor.FromArgb(64, 158, 123, 240);
        public override DColor MenuItemBorder => DColor.FromArgb(97, 158, 123, 240);
        public override DColor MenuItemSelectedGradientBegin => MenuItemSelected;
        public override DColor MenuItemSelectedGradientEnd => MenuItemSelected;
        public override DColor ToolStripDropDownBackground => DColor.FromArgb(30, 28, 38);
        public override DColor ImageMarginGradientBegin => DColor.FromArgb(30, 28, 38);
        public override DColor ImageMarginGradientMiddle => DColor.FromArgb(30, 28, 38);
        public override DColor ImageMarginGradientEnd => DColor.FromArgb(30, 28, 38);
    }
}
