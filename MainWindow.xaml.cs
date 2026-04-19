using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clipboarder.Models;
using Clipboarder.Services;
using Clipboarder.ViewModels;

namespace Clipboarder;

public partial class MainWindow : Window
{
    public MainViewModel VM { get; }
    private ClipboardMonitor? _clipboardMonitor;
    private HotkeyService? _hotkey;
    private string? _lastClipboardText;
    private IntPtr _targetHwnd = IntPtr.Zero;
    private bool _allowClose;
    // Screenshot tools (Snipping Tool, browsers, etc.) frequently push the clipboard twice
    // for a single capture. Same-dimension images arriving within this window are ignored.
    private int _lastImgW, _lastImgH;
    private DateTime _lastImgTs = DateTime.MinValue;
    // Blanket-ignore all clipboard events until this deadline — set whenever we write
    // to the clipboard ourselves, so our own Copy/Paste never loops back into history.
    private DateTime _suppressUntil = DateTime.MinValue;
    // Cached at OnLoaded so every Ctrl+C doesn't re-read settings.json off disk.
    private IReadOnlyList<string> _blocklist = CaptureRules.DefaultBlockedProcesses;

    public event Action<bool>? PauseCaptureChanged;
    // Raised every time the window is surfaced (hotkey, tray click). App listens
    // to fire an opportunistic update check — catches releases published while the
    // app was sitting in the tray for days.
    public event Action? WindowShown;

    public MainWindow()
    {
        InitializeComponent();
        VM = new MainViewModel(useSeedData: false);
        DataContext = VM;
        VM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VM.IsCapturePaused))
                PauseCaptureChanged?.Invoke(VM.IsCapturePaused);
        };
        // Suppress any clipboard event for the next 800ms so our own Copy/Paste writes
        // — text, images, anything — don't loop back through the capture pipeline.
        VM.BeforeSelfClipboardWrite += _ =>
            _suppressUntil = DateTime.Now.AddMilliseconds(800);
        Loaded += OnLoaded;
        Closed += OnClosed;
        Deactivated += OnWindowDeactivated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _clipboardMonitor = new ClipboardMonitor(this);
        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;

        try
        {
            _hotkey = new HotkeyService(this, ModifierKeys.Control | ModifierKeys.Shift, Key.V);
            _hotkey.Pressed += OnHotkeyPressed;
        }
        catch { }

        // Prime _lastClipboardText so whatever is currently on the clipboard when
        // the app starts doesn't get captured as a new item on first change.
        try { _lastClipboardText = Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { }

        // settings.BlockedProcesses == null → fall back to defaults; explicit empty
        // list means the user disabled the feature.
        var s = SettingsStore.Load();
        _blocklist = s.BlockedProcesses is { } list
            ? (IReadOnlyList<string>)list
            : CaptureRules.DefaultBlockedProcesses;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _clipboardMonitor?.Dispose();
        _hotkey?.Dispose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    public void AllowClose() => _allowClose = true;

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (!IsVisible) return;
        // Keep the window up while a modal child (e.g. ConfirmDialog) steals focus.
        foreach (Window w in OwnedWindows)
            if (w.IsVisible) return;
        HideToTray();
    }

    public void ShowFromTray()
    {
        var ourHwnd = new WindowInteropHelper(this).Handle;
        var fg = PasteService.GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != ourHwnd) _targetHwnd = fg;

        PositionForAnchor();

        ShowInTaskbar = true;
        if (!IsVisible || Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            Show();
        }
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        // Flash Topmost to force z-order to the front; Activate() alone is unreliable
        // when another app was foreground (Windows throttles foreground changes).
        Topmost = true; Topmost = false;

        Dispatcher.BeginInvoke(() =>
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }, DispatcherPriority.Input);

        WindowShown?.Invoke();
    }

    // Anchors the window next to the focused input's caret. Falls back to the
    // foreground window's center, then to the primary monitor's center.
    private void PositionForAnchor()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        if (InputAnchor.TryGetCaret() is System.Windows.Point caret)
        {
            PlaceAtPhysicalPoint(caret.X, caret.Y, scale, belowAnchor: true, gap: 10);
            return;
        }

        if (InputAnchor.TryGetForegroundWindowRect() is System.Windows.Rect fgRect)
        {
            PlaceAtPhysicalPoint(fgRect.X + fgRect.Width / 2, fgRect.Y + fgRect.Height / 2,
                                 scale, belowAnchor: false, gap: 0, centered: true);
            return;
        }

        // Last-ditch: primary screen center.
        var sb = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                 ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        Left = sb.Left / scale + (sb.Width / scale - Width) / 2;
        Top  = sb.Top  / scale + (sb.Height / scale - Height) / 2;
    }

    private void PlaceAtPhysicalPoint(double px, double py, double scale,
                                      bool belowAnchor, double gap, bool centered = false)
    {
        var workPhys = System.Windows.Forms.Screen
            .FromPoint(new System.Drawing.Point((int)px, (int)py)).WorkingArea;
        var wl = workPhys.Left  / scale;
        var wt = workPhys.Top   / scale;
        var wr = workPhys.Right / scale;
        var wb = workPhys.Bottom / scale;

        var ax = px / scale;
        var ay = py / scale;

        double left = centered ? ax - Width / 2 : ax - 48;
        double top  = centered ? ay - Height / 2 : ay + gap;

        if (left + Width > wr) left = wr - Width - 8;
        if (left < wl) left = wl + 8;

        if (top + Height > wb)
        {
            // Not enough room below; try placing above the anchor instead.
            top = belowAnchor ? (ay - Height - gap) : wb - Height - 8;
        }
        if (top < wt) top = wt + 8;

        Left = left;
        Top  = top;
    }

    private void HideToTray()
    {
        if (!IsVisible) return;
        Hide();
    }

    private void OnHotkeyPressed()
    {
        if (IsVisible && IsActive) HideToTray();
        else ShowFromTray();
    }

    private void OnClipboardChanged()
    {
        try
        {
            if (VM.IsCapturePaused) return;
            // Drop events that fire as a consequence of our own Copy/Paste writes.
            if (DateTime.Now < _suppressUntil) return;
            // Capture rules gate: standard exclusion formats + per-app blocklist.
            // Runs before any clipboard content read, so secrets don't touch memory.
            if (CaptureRules.ShouldSkip(_blocklist)) return;
            var sourceName = DetectSourceName();

            // Text first — covers the common case and supports dedup via _lastClipboardText.
            if (Clipboard.ContainsText())
            {
                var txt = Clipboard.GetText();
                if (string.IsNullOrEmpty(txt) || txt == _lastClipboardText) return;
                _lastClipboardText = txt;
                var it = BuildFromText(txt, sourceName);
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () => VM.AddIncoming(it));
                return;
            }

            // Screenshots / copied images land here (CF_BITMAP / CF_DIB).
            if (Clipboard.ContainsImage())
            {
                var bmp = Clipboard.GetImage();
                if (bmp is null) return;

                var now = DateTime.Now;
                if (bmp.PixelWidth == _lastImgW && bmp.PixelHeight == _lastImgH
                    && (now - _lastImgTs).TotalMilliseconds < 1200) return;
                _lastImgW  = bmp.PixelWidth;
                _lastImgH  = bmp.PixelHeight;
                _lastImgTs = now;

                var it = BuildFromImage(bmp, sourceName);
                if (it is not null)
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, () => VM.AddIncoming(it));
            }
        }
        catch { }
    }

    private static string BlobsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Clipboarder", "blobs");

    private static ClipItem? BuildFromImage(BitmapSource bmp, string source)
    {
        try
        {
            Directory.CreateDirectory(BlobsDir);
            var name = $"clip-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png";
            var path = Path.Combine(BlobsDir, name);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) encoder.Save(fs);
            var size = new FileInfo(path).Length;
            return new ClipItem
            {
                Type = ClipType.Image,
                Content = path,
                Source = source,
                Width = bmp.PixelWidth,
                Height = bmp.PixelHeight,
                FileSize = HumanSize(size),
            };
        }
        catch { return null; }
    }

    private static string HumanSize(long b) => b switch
    {
        < 1024       => $"{b} B",
        < 1024*1024  => $"{b / 1024.0:0.#} KB",
        < 1024L*1024*1024 => $"{b / 1048576.0:0.#} MB",
        _            => $"{b / 1073741824.0:0.##} GB",
    };

    private string DetectSourceName()
    {
        var ourHwnd = new WindowInteropHelper(this).Handle;
        var fg = PasteService.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ourHwnd) return "Clipboard";
        var name = PasteService.GetProcessName(fg);
        return string.IsNullOrWhiteSpace(name) ? "Clipboard" : PrettyProcessName(name!);
    }

    private static string PrettyProcessName(string raw) => raw switch
    {
        "chrome" => "Chrome",
        "msedge" => "Edge",
        "firefox" => "Firefox",
        "Code" or "code" => "VS Code",
        "devenv" => "Visual Studio",
        "WindowsTerminal" => "Terminal",
        "explorer" => "Explorer",
        "notepad" => "Notepad",
        "OUTLOOK" => "Outlook",
        _ => char.ToUpper(raw[0]) + raw[1..],
    };

    private static ClipItem BuildFromText(string txt, string source)
    {
        var trimmed = txt.Trim();
        // High-signal single-line classifications take priority over code detection.
        if (ColorDetector.TryDetect(trimmed, out var normalizedColor))
            return new ClipItem { Type = ClipType.Color, Content = normalizedColor, Source = source };
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            return new ClipItem { Type = ClipType.Link, Content = trimmed, Source = source };
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return new ClipItem { Type = ClipType.Email, Content = trimmed, Source = source };
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d{4,8}$"))
            return new ClipItem { Type = ClipType.Text, Content = trimmed, Source = source, Tag = "2FA" };

        var code = CodeDetector.Detect(txt);
        if (code.IsCode)
            return new ClipItem { Type = ClipType.Code, Content = txt, Source = source, Lang = code.Lang ?? "txt" };

        return new ClipItem { Type = ClipType.Text, Content = txt, Source = source };
    }

    private void OnDragTopbar(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove();
    }

    // Quick-section "History" row: switch to the All category and clear any active filter.
    private void OnHistoryClick(object sender, MouseButtonEventArgs e)
    {
        var all = VM.Categories.FirstOrDefault(c => c.Id == "all");
        if (all is not null) VM.SelectedCategory = all;
        VM.Query = "";
        e.Handled = true;
    }

    private void OnClearCategoryClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ViewModels.CategoryRowVM cat)
            VM.ClearCategory(cat);
        e.Handled = true;
    }

    private void OnCardLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ClipItem it)
        {
            PasteItem(it);
            e.Handled = true;
        }
    }

    private void PasteItem(ClipItem it)
    {
        _suppressUntil = DateTime.Now.AddMilliseconds(800);
        try
        {
            if (it.Type == ClipType.Image && File.Exists(it.Content))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(it.Content);
                bmp.EndInit();
                bmp.Freeze();
                Clipboard.SetImage(bmp);
            }
            else
            {
                _lastClipboardText = it.Content;
                Clipboard.SetText(it.Content);
            }
        }
        catch { }

        VM.ShowToastMessage($"Pasted · {it.Source}");
        it.Timestamp = DateTime.Now;
        VM.UpdateCounts();
        VM.FlushPersistDebounced();

        var target = _targetHwnd;
        HideToTray();

        Dispatcher.BeginInvoke(() => PasteService.RestoreFocusAndPaste(target),
            DispatcherPriority.Background);
    }

    private void OnSearchFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Border b) return;
        var focused = (bool)e.NewValue;
        var bg = (SolidColorBrush)b.Background;
        var bd = (SolidColorBrush)b.BorderBrush;

        var toBg = Color.FromArgb(focused ? (byte)0x1F : (byte)0x14, 0xFF, 0xFF, 0xFF);
        var toBd = focused
            ? (Color)ColorConverter.ConvertFromString("#619E7BF0")
            : (Color)ColorConverter.ConvertFromString("#0FFFFFFF");

        bg.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(toBg, TimeSpan.FromMilliseconds(180)));
        bd.BeginAnimation(SolidColorBrush.ColorProperty,
            new ColorAnimation(toBd, TimeSpan.FromMilliseconds(180)));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.OemQuestion && !SearchBox.IsKeyboardFocused)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(VM.Query))
            {
                VM.Query = "";
                e.Handled = true;
                return;
            }
            HideToTray();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.P)
        {
            var it = FirstVisibleItem();
            if (it is not null) VM.PinCommand.Execute(it);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && SearchBox.IsKeyboardFocused)
        {
            var it = FirstVisibleItem();
            if (it is not null) { PasteItem(it); e.Handled = true; return; }
        }

        base.OnPreviewKeyDown(e);
    }

    private ClipItem? FirstVisibleItem()
    {
        foreach (var o in VM.ItemsView)
            if (o is ClipItem c) return c;
        return null;
    }
}
