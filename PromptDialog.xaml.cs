using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Clipboarder;

public partial class PromptDialog : Window
{
    public sealed class Field : INotifyPropertyChanged
    {
        public string Label { get; init; } = "";
        private string _value = "";
        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnChanged(); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n ?? ""));
    }

    public ObservableCollection<Field> Fields { get; } = new();

    public PromptDialog(IEnumerable<string> labels)
    {
        InitializeComponent();
        foreach (var l in labels) Fields.Add(new Field { Label = l });
        FieldsHost.ItemsSource = Fields;
    }

    // Modal-style helper: blocks, returns a label→value map on confirm or null on cancel.
    public static IDictionary<string, string>? Show(Window? owner, IEnumerable<string> labels)
    {
        var list = labels.ToList();
        if (list.Count == 0) return new Dictionary<string, string>();

        var dlg = new PromptDialog(list);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;
        if (dlg.ShowDialog() != true) return null;

        var dict = new Dictionary<string, string>();
        foreach (var f in dlg.Fields) dict[f.Label] = f.Value;
        return dict;
    }

    // Focus the first field for immediate typing — otherwise the user has to
    // click into the dialog, which defeats the point of a keyboard-first flow.
    private bool _firstFocused;
    private void OnFieldLoaded(object sender, RoutedEventArgs e)
    {
        if (_firstFocused) return;
        if (sender is System.Windows.Controls.TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
            _firstFocused = true;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e)  { DialogResult = false; Close(); }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
