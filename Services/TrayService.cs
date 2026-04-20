using System.Drawing;
using System.Windows.Forms;
using DColor = System.Drawing.Color;

namespace Clipboarder.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;

    public event Action? ShowRequested;
    public event Action? QuitRequested;
    public event Action? ClearRequested;
    public event Action? SettingsRequested;
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

        var settingsItem = new ToolStripMenuItem("Settings\u2026");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(clearItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(quitItem);

        _icon.ContextMenuStrip = menu;
    }

    public void SetPaused(bool paused)
    {
        if (_pauseItem.Checked != paused) _pauseItem.Checked = paused;
    }

    private static Icon BuildIcon()
    {
        // Load the multi-resolution .ico bundled as a WPF resource; the
        // Icon ctor picks the frame matching the system's small-icon size,
        // so the tray stays crisp on high-DPI displays.
        var uri = new Uri("pack://application:,,,/assets/icon.ico", UriKind.Absolute);
        var res = System.Windows.Application.GetResourceStream(uri);
        using var stream = res.Stream;
        var target = SystemInformation.SmallIconSize;
        return new Icon(stream, target.Width, target.Height);
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
