using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clipboarder.Services;

namespace Clipboarder;

public partial class SettingsWindow : Window
{
    private const string DefaultHotkey = "Ctrl+Shift+V";

    // Compiled once per edit — we recompile the pattern list whenever the
    // user types in the Patterns box so the live tester reflects the in-flight
    // state, not what's currently saved.
    private IReadOnlyList<Regex> _compiledPatterns = Array.Empty<Regex>();

    // Hotkey recorder state. We don't write directly into settings on every
    // keystroke — the user might be mid-combo when they change their mind.
    // Commit on Save; Esc reverts, Backspace resets to default.
    private bool _recordingHotkey;
    private ModifierKeys _pendingHotkeyMods = ModifierKeys.Control | ModifierKeys.Shift;
    private Key _pendingHotkeyKey = Key.V;

    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => PrivacyService.ApplyFromSettings(this);
        Load();
    }

    private void Load()
    {
        var s = SettingsStore.Load();

        AutoStartCheck.IsChecked = AutoStartService.IsRegistered();
        HideFromCaptureCheck.IsChecked = s.HideFromScreenCapture == true;

        TwoFaTtlBox.Text    = (s.TwoFactorTtlSeconds ?? 60).ToString();
        UnpinnedTtlBox.Text = (s.UnpinnedTtlDays     ?? 0).ToString();
        MaxUnpinnedBox.Text = (s.MaxUnpinnedItems    ?? 0).ToString();

        ProcessesBox.Text = JoinLines(s.BlockedProcesses ?? CaptureRules.DefaultBlockedProcesses);
        PatternsBox.Text  = JoinLines(s.BlockedPatterns  ?? CaptureRules.DefaultBlockedPatterns);

        if (HotkeyParser.TryParse(s.OpenWindowHotkey, out var hm, out var hk))
        {
            _pendingHotkeyMods = hm;
            _pendingHotkeyKey  = hk;
        }
        HotkeyText.Text = HotkeyParser.Format(_pendingHotkeyMods, _pendingHotkeyKey);

        VersionText.Text = "Advanced Clipboarder v" + AppVersion();

        RecompilePatternsFromBox();
    }

    private static string JoinLines(IEnumerable<string> xs) =>
        string.Join(Environment.NewLine, xs);

    private static string AppVersion()
    {
        var ver = UpdateService.CurrentVersion();
        return ver?.ToString(3) ?? "unknown";
    }

    private static int ParseInt(string text, int fallback)
    {
        var t = (text ?? "").Trim();
        return int.TryParse(t, out var v) && v >= 0 ? v : fallback;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var processes = SplitLines(ProcessesBox.Text);
        var patterns  = SplitLines(PatternsBox.Text);

        // Block Save if any pattern is invalid — otherwise the user thinks
        // they're protected but the regex was silently dropped at load time.
        var bad = FindInvalidPattern(patterns);
        if (bad is not null)
        {
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("Danger");
            StatusText.Text = $"Invalid regex: {Truncate(bad, 40)}";
            return;
        }

        SettingsStore.Update(s =>
        {
            s.TwoFactorTtlSeconds = ParseInt(TwoFaTtlBox.Text, 60);
            s.UnpinnedTtlDays     = ParseInt(UnpinnedTtlBox.Text, 0);
            s.MaxUnpinnedItems    = ParseInt(MaxUnpinnedBox.Text, 0);
            s.BlockedProcesses    = processes.ToList();
            s.BlockedPatterns     = patterns.ToList();
            s.OpenWindowHotkey    = HotkeyParser.Format(_pendingHotkeyMods, _pendingHotkeyKey);
            s.HideFromScreenCapture = HideFromCaptureCheck.IsChecked == true;
        });

        if (AutoStartCheck.IsChecked == true) AutoStartService.EnsureRegistered();
        else                                  AutoStartService.Unregister();

        SettingsSaved?.Invoke(this, EventArgs.Empty);
        DialogResult = true;
        Close();
    }

    // MainWindow listens so its cached blocklist + pattern array stay in sync
    // with what the user just saved. Without this, capture rules only update
    // on next app launch.
    public static event EventHandler? SettingsSaved;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Replace("\r\n", "\n").Split('\n')
                   .Select(l => l.Trim())
                   .Where(l => l.Length > 0)
                   .ToArray();
    }

    private static string? FindInvalidPattern(IEnumerable<string> patterns)
    {
        foreach (var p in patterns)
        {
            try { _ = new Regex(p); }
            catch { return p; }
        }
        return null;
    }

    private static string Truncate(string s, int n) =>
        s.Length <= n ? s : s[..n] + "…";

    private void OnResetProcesses(object sender, RoutedEventArgs e)
    {
        ProcessesBox.Text = JoinLines(CaptureRules.DefaultBlockedProcesses);
    }

    private void OnResetPatterns(object sender, RoutedEventArgs e)
    {
        PatternsBox.Text = JoinLines(CaptureRules.DefaultBlockedPatterns);
        RecompilePatternsFromBox();
        UpdateTestResult();
    }

    private void OnPatternsChanged(object sender, TextChangedEventArgs e)
    {
        RecompilePatternsFromBox();
        UpdateTestResult();
    }

    private void OnTestTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTestResult();
    }

    private void RecompilePatternsFromBox()
    {
        _compiledPatterns = CaptureRules.CompilePatterns(SplitLines(PatternsBox?.Text));
    }

    private void UpdateTestResult()
    {
        var txt = PatternTestBox?.Text ?? "";
        if (string.IsNullOrEmpty(txt))
        {
            PatternTestResult.Text = "type to test";
            PatternTestResult.Foreground = (System.Windows.Media.Brush)FindResource("Fg3");
            return;
        }
        int hits = 0;
        foreach (var re in _compiledPatterns)
        {
            try { if (re.IsMatch(txt.Trim())) hits++; } catch { }
        }
        if (hits == 0)
        {
            PatternTestResult.Text = "no match · would capture";
            PatternTestResult.Foreground = (System.Windows.Media.Brush)FindResource("Fg3");
        }
        else
        {
            PatternTestResult.Text = $"matches {hits} · would block";
            PatternTestResult.Foreground = (System.Windows.Media.Brush)FindResource("Warn");
        }
    }

    // Limit numeric fields to digits so users can't type letters into them.
    // Real validation (bounds, signed values) happens in ParseInt at save time.
    private void OnNumericInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
            if (!char.IsDigit(ch)) { e.Handled = true; return; }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnHotkeyClick(object sender, MouseButtonEventArgs e)
    {
        HotkeyBox.Focus();
        BeginRecording();
        e.Handled = true;
    }

    private void BeginRecording()
    {
        _recordingHotkey = true;
        HotkeyText.Text = "Press keys…";
        HotkeyBox.BorderBrush = (Brush)FindResource("Accent");
    }

    private void StopRecording()
    {
        _recordingHotkey = false;
        HotkeyText.Text = HotkeyParser.Format(_pendingHotkeyMods, _pendingHotkeyKey);
        HotkeyBox.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recordingHotkey) return;

        // When Alt is held, WPF routes the combo through SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape reverts to whatever was showing before recording started.
        if (key == Key.Escape)
        {
            StopRecording();
            e.Handled = true;
            return;
        }

        // Backspace alone resets to the built-in default so the user can
        // bail out of a bad combo without having to remember the old one.
        if (key == Key.Back && Keyboard.Modifiers == ModifierKeys.None)
        {
            HotkeyParser.TryParse(DefaultHotkey, out _pendingHotkeyMods, out _pendingHotkeyKey);
            StopRecording();
            e.Handled = true;
            return;
        }

        // Ignore the moment the user is still pressing only modifiers —
        // we want to see the non-modifier key they pair with them.
        if (IsPureModifier(key))
        {
            HotkeyText.Text = HotkeyParser.Format(Keyboard.Modifiers, Key.None) + "+…";
            e.Handled = true;
            return;
        }

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None)
        {
            // Unmodified key — global hotkeys must carry a modifier or they
            // steal a keystroke from every app on the system.
            HotkeyText.Text = "Add a modifier (Ctrl / Shift / Alt / Win)";
            e.Handled = true;
            return;
        }

        _pendingHotkeyMods = mods;
        _pendingHotkeyKey  = key;
        StopRecording();
        e.Handled = true;
    }

    private static bool IsPureModifier(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift
        or Key.LeftAlt  or Key.RightAlt
        or Key.LWin     or Key.RWin;
}
