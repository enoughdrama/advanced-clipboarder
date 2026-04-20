using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Clipboarder;

public enum ConfirmKind
{
    // Red trash glyph, red CTA — destructive action like delete / clear history.
    Destructive,
    // Purple download glyph, accent CTA — affirmative like install update.
    Primary,
}

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmLabel = "Delete",
                         ConfirmKind kind = ConfirmKind.Destructive)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Services.PrivacyService.ApplyFromSettings(this);
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmBtn.Content = confirmLabel;
        ApplyKind(kind);
    }

    private void ApplyKind(ConfirmKind kind)
    {
        switch (kind)
        {
            case ConfirmKind.Primary:
                // Accent purple from Theme.xaml.
                var accent       = (Color)ColorConverter.ConvertFromString("#9E7BF0")!;
                var accentHover  = (Color)ColorConverter.ConvertFromString("#B09AF5")!;
                var accentPress  = (Color)ColorConverter.ConvertFromString("#8E6BE0")!;
                var accentSoft   = (Color)ColorConverter.ConvertFromString("#1F9E7BF0")!;

                ConfirmBtn.Resources["ConfirmAccent"]        = new SolidColorBrush(accent);
                ConfirmBtn.Resources["ConfirmAccentHover"]   = new SolidColorBrush(accentHover);
                ConfirmBtn.Resources["ConfirmAccentPressed"] = new SolidColorBrush(accentPress);

                IconSwatch.Background = new SolidColorBrush(accentSoft);
                IconPath.Stroke       = new SolidColorBrush(accent);
                IconPath.Data         = (Geometry)FindResource("GeomDownload");
                break;

            case ConfirmKind.Destructive:
            default:
                // The XAML defaults already paint destructive red + trash, so nothing to do.
                break;
        }
    }

    public static bool Show(Window? owner, string title, string message,
                            string confirmLabel = "Delete",
                            ConfirmKind kind = ConfirmKind.Destructive)
    {
        var dlg = new ConfirmDialog(title, message, confirmLabel, kind);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e)  { DialogResult = false; Close(); }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
