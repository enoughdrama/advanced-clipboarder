using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Clipboarder.Services;

// Bound-to view model for the link hover preview. Starts out with
// IsLoading=true; LinkPreviewService fills Title / ImagePath in the
// background and raises PropertyChanged on the UI dispatcher.
public sealed class LinkPreview : INotifyPropertyChanged
{
    private string? _title;
    public string? Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnChanged(); } }
    }

    private string? _imagePath;
    public string? ImagePath
    {
        get => _imagePath;
        set { if (_imagePath != value) { _imagePath = value; OnChanged(); } }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}
