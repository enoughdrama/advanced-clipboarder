using System.Windows;
using System.Windows.Input;

namespace Clipboarder;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmLabel = "Delete")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmBtn.Content = confirmLabel;
    }

    public static bool Show(Window? owner, string title, string message, string confirmLabel = "Delete")
    {
        var dlg = new ConfirmDialog(title, message, confirmLabel);
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
