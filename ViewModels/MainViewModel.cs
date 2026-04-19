using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Clipboarder.Models;
using Clipboarder.Services;

namespace Clipboarder.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ClipItem> Items { get; }
    public ICollectionView ItemsView { get; }
    public ObservableCollection<CategoryRowVM> Categories { get; }

    private string _query = "";
    public string Query
    {
        get => _query;
        set { if (_query != value) { _query = value; OnChanged(); ItemsView.Refresh(); UpdateCounts(); } }
    }

    private CategoryRowVM _selectedCategory = null!;
    public CategoryRowVM SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value) return;
            _selectedCategory = value;
            OnChanged();
            ItemsView.Refresh();
            OnChanged(nameof(FilteredCount));
            SettingsStore.Update(s => s.LastCategoryId = value?.Id);
        }
    }

    private ClipItem? _selectedItem;
    public ClipItem? SelectedItem
    {
        get => _selectedItem;
        set { if (_selectedItem != value) { _selectedItem = value; OnChanged(); } }
    }

    private string _toast = "";
    public string Toast
    {
        get => _toast;
        private set { _toast = value; OnChanged(); OnChanged(nameof(ToastVisible)); }
    }
    public bool ToastVisible => !string.IsNullOrEmpty(_toast);

    private bool _isCapturePaused;
    public bool IsCapturePaused
    {
        get => _isCapturePaused;
        set { if (_isCapturePaused != value) { _isCapturePaused = value; OnChanged(); ShowToast(value ? "Capture paused" : "Capture resumed"); } }
    }

    public int FilteredCount
    {
        get
        {
            int n = 0; foreach (var _ in ItemsView) n++;
            return n;
        }
    }

    public int PinnedCount => Categories.FirstOrDefault(c => c.Id == "pinned")?.Count ?? 0;
    public int TotalCount => Items.Count;

    public ICommand CopyCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand SelectCategoryCommand { get; }

    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _persistTimer;

    public MainViewModel() : this(useSeedData: false) { }

    public MainViewModel(bool useSeedData)
    {
        // Prefer persisted history over seed data when the file exists.
        var persisted = HistoryStore.Load();
        IEnumerable<ClipItem> initial = persisted.Count > 0
            ? persisted
            : (useSeedData ? SeedData.Build() : Array.Empty<ClipItem>());
        Items = new ObservableCollection<ClipItem>(initial);
        Categories = new ObservableCollection<CategoryRowVM>(
            ClipCategory.All.Select(c => new CategoryRowVM(c)));

        var settings = SettingsStore.Load();
        _selectedCategory = Categories.FirstOrDefault(c => c.Id == settings.LastCategoryId)
                            ?? Categories[0];

        var view = CollectionViewSource.GetDefaultView(Items);
        view.Filter = FilterItem;
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(nameof(ClipItem.Pinned), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(ClipItem.Timestamp), ListSortDirection.Descending));
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipItem.GroupKey)));

        // Without live shaping, pinning an item on one tab wouldn't surface in the
        // Pinned tab until a manual ItemsView.Refresh().
        if (view is ICollectionViewLiveShaping live)
        {
            live.IsLiveFiltering = true;
            live.IsLiveSorting = true;
            live.IsLiveGrouping = true;
            live.LiveFilteringProperties.Add(nameof(ClipItem.Pinned));
            live.LiveFilteringProperties.Add(nameof(ClipItem.Type));
            live.LiveSortingProperties.Add(nameof(ClipItem.Pinned));
            live.LiveSortingProperties.Add(nameof(ClipItem.Timestamp));
            live.LiveGroupingProperties.Add(nameof(ClipItem.GroupKey));
        }

        ItemsView = view;

        SelectedItem = Items.FirstOrDefault();

        CopyCommand  = new RelayCommand(p => DoCopy(p as ClipItem ?? SelectedItem));
        PinCommand   = new RelayCommand(p => DoPin(p as ClipItem ?? SelectedItem));
        DeleteCommand = new RelayCommand(p => DoDelete(p as ClipItem ?? SelectedItem));
        PasteCommand = new RelayCommand(p => DoPaste(p as ClipItem ?? SelectedItem));
        ClearSearchCommand = new RelayCommand(() => Query = "");
        SelectCategoryCommand = new RelayCommand(p => { if (p is CategoryRowVM c) SelectedCategory = c; });

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast = ""; };

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => { foreach (var it in Items) it.Refresh(); ItemsView.Refresh(); };
        _refreshTimer.Start();

        // Debounced save — coalesces bursts of mutations into one file write.
        _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _persistTimer.Tick += (_, _) => { _persistTimer.Stop(); HistoryStore.Save(Items); };

        UpdateCounts();
    }

    // Debounced — coalesces a burst of changes into one file write 500ms later.
    public void FlushPersistDebounced()
    {
        _persistTimer.Stop();
        _persistTimer.Start();
    }

    // Synchronous flush for app shutdown — bypasses the debounce.
    public void FlushPersist()
    {
        _persistTimer.Stop();
        HistoryStore.Save(Items);
    }

    private bool FilterItem(object o)
    {
        if (o is not ClipItem it) return false;
        if (!SelectedCategory.Category.Match(it)) return false;
        if (string.IsNullOrWhiteSpace(_query)) return true;
        var q = _query.Trim();
        return it.Content.Contains(q, StringComparison.OrdinalIgnoreCase)
            || it.Source.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateCounts()
    {
        foreach (var cat in Categories)
            cat.Count = Items.Count(cat.Category.Match);
        OnChanged(nameof(FilteredCount));
        OnChanged(nameof(PinnedCount));
        OnChanged(nameof(TotalCount));
    }

    public void AddIncoming(ClipItem item)
    {
        if (_isCapturePaused) return;

        // Dedup: if same text as current top item, just bump timestamp
        var top = Items.FirstOrDefault(x => !x.Pinned);
        if (top is not null && top.Type == item.Type && top.Content == item.Content)
        {
            top.Timestamp = DateTime.Now;
            top.IsFresh = true;
            FlashOff(top);
            ItemsView.Refresh();
            FlushPersistDebounced();
            return;
        }

        Items.Insert(0, item);
        item.IsFresh = true;
        FlashOff(item);
        ItemsView.Refresh();
        UpdateCounts();
        FlushPersistDebounced();
    }

    private static void FlashOff(ClipItem item)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        t.Tick += (_, _) => { t.Stop(); item.IsFresh = false; };
        t.Start();
    }

    public void ClearHistory()
    {
        if (Items.Count == 0) return;
        if (!Confirm($"Clear all {Items.Count} entries from history?", "Clear history")) return;
        Items.Clear();
        SelectedItem = null;
        UpdateCounts();
        FlushPersistDebounced();
        ShowToast("History cleared");
    }

    // Clears every item matching the category's predicate; "all" empties the history.
    public void ClearCategory(CategoryRowVM cat)
    {
        if (cat is null) return;
        if (cat.Id == "all") { ClearHistory(); return; }

        var doomed = Items.Where(cat.Category.Match).ToList();
        if (doomed.Count == 0) return;

        if (!Confirm($"Clear all {doomed.Count} entries in {cat.Label}?", $"Clear {cat.Label}")) return;

        foreach (var it in doomed) Items.Remove(it);
        if (SelectedItem is not null && doomed.Contains(SelectedItem)) SelectedItem = null;
        UpdateCounts();
        FlushPersistDebounced();
        ShowToast($"Cleared {cat.Label} \u00B7 {doomed.Count}");
    }

    private static bool Confirm(string message, string title)
        => ConfirmDialog.Show(System.Windows.Application.Current?.MainWindow, title, message);

    public void ShowToastMessage(string msg) => ShowToast(msg);

    // Raised just before we push content to the system clipboard ourselves.
    // MainWindow listens so ClipboardMonitor doesn't re-capture our own writes.
    public event Action<ClipItem>? BeforeSelfClipboardWrite;

    private void DoCopy(ClipItem? it)
    {
        if (it is null) return;
        BeforeSelfClipboardWrite?.Invoke(it);
        try
        {
            if (it.Type == ClipType.Image && System.IO.File.Exists(it.Content))
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(it.Content);
                bmp.EndInit();
                bmp.Freeze();
                System.Windows.Clipboard.SetImage(bmp);
            }
            else
            {
                System.Windows.Clipboard.SetText(it.Content);
            }
        }
        catch { }
        ShowToast($"Copied \u00B7 {it.TypeLabel}");
    }

    private void DoPin(ClipItem? it)
    {
        if (it is null) return;
        it.Pinned = !it.Pinned;
        ItemsView.Refresh();
        UpdateCounts();
        FlushPersistDebounced();
        ShowToast(it.Pinned ? "Pinned" : "Unpinned");
    }

    private void DoDelete(ClipItem? it)
    {
        if (it is null) return;
        if (!Confirm("Delete this entry?", "Delete entry")) return;
        var idx = Items.IndexOf(it);
        Items.Remove(it);
        UpdateCounts();
        if (Items.Count > 0)
            SelectedItem = Items[Math.Min(idx, Items.Count - 1)];
        else
            SelectedItem = null;
        FlushPersistDebounced();
        ShowToast("Removed");
    }

    private void DoPaste(ClipItem? it)
    {
        if (it is null) return;
        try { System.Windows.Clipboard.SetText(it.Content); } catch { }
        it.Timestamp = DateTime.Now;
        it.IsFresh = true;
        var flash = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        flash.Tick += (_, _) => { flash.Stop(); it.IsFresh = false; };
        flash.Start();
        ItemsView.Refresh();
        FlushPersistDebounced();
        ShowToast($"Pasted \u00B7 {it.Source}");
    }

    private void ShowToast(string msg)
    {
        Toast = msg;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}
