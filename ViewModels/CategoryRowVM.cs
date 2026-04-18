using System.ComponentModel;
using System.Runtime.CompilerServices;
using Clipboarder.Models;

namespace Clipboarder.ViewModels;

public class CategoryRowVM : INotifyPropertyChanged
{
    public ClipCategory Category { get; }
    public string Id => Category.Id;
    public string Label => Category.Label;
    public string IconKey => Category.IconKey;

    private int _count;
    public int Count
    {
        get => _count;
        set { if (_count != value) { _count = value; OnChanged(); } }
    }

    public CategoryRowVM(ClipCategory c) { Category = c; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n ?? ""));
}
