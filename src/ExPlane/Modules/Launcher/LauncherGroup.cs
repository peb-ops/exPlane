using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using ExPlane.Services;

namespace ExPlane.Modules.Launcher;

/// <summary>
/// A named, collapsible group of launcher items. With a PortalPath it is a folder portal:
/// its items mirror the folder's contents live and aren't stored in settings.
/// </summary>
public sealed class LauncherGroup : INotifyPropertyChanged, IDisposable
{
    private readonly Action _persist;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer? _reloadTimer;
    private FileSystemWatcher? _watcher;
    private string _name;
    private bool _isCollapsed;
    private bool _isListView;
    private bool _isDropTarget;
    private string _dropHint = "";

    public LauncherGroup(string name, bool isCollapsed, string? portalPath, Action persist, bool isListView = false)
    {
        _name = name;
        _isCollapsed = isCollapsed;
        _isListView = isListView;
        _persist = persist;
        PortalPath = portalPath;
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CanOpenAll));
        };

        if (IsPortal)
        {
            // Explorer operations fire bursts of events; reload once things settle.
            _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _reloadTimer.Tick += (_, _) =>
            {
                _reloadTimer.Stop();
                ReloadPortal();
            };
            ReloadPortal();
            StartWatching();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LauncherItem> Items { get; } = new();

    public string? PortalPath { get; }

    public bool IsPortal => PortalPath is not null;

    public bool CanEditItems => !IsPortal;

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Not for portals: opening every file in e.g. Downloads at once is never what you want.</summary>
    public bool CanOpenAll => !IsPortal && !IsEmpty;

    /// <summary>Compact rows (icon, title, site) instead of icon tiles. Saved per group.</summary>
    public bool IsListView
    {
        get => _isListView;
        set
        {
            if (value == _isListView) return;
            _isListView = value;
            OnPropertyChanged();
            _persist();
        }
    }

    public string CountText => Items.Count.ToString();

    /// <summary>Something droppable is being dragged over this group.</summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        private set
        {
            if (value == _isDropTarget) return;
            _isDropTarget = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Chip text while dragging, e.g. "Add 2 here" / "Move here".</summary>
    public string DropHint
    {
        get => _dropHint;
        private set
        {
            if (value == _dropHint) return;
            _dropHint = value;
            OnPropertyChanged();
        }
    }

    public void ShowDrop(string hint)
    {
        DropHint = hint;
        IsDropTarget = true;
    }

    public void HideDrop() => IsDropTarget = false;

    public string EmptyHint => !IsPortal ? "Drop files here, or right-click the title to add"
        : Directory.Exists(PortalPath) ? "Folder is empty"
        : "Folder not found";

    public string Name
    {
        get => _name;
        set
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed == _name)
            {
                // Rejected or unchanged: re-raise so the TextBox snaps back.
                OnPropertyChanged();
                return;
            }
            _name = trimmed;
            OnPropertyChanged();
            _persist();
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (value == _isCollapsed) return;
            _isCollapsed = value;
            OnPropertyChanged();
            _persist();
        }
    }

    public LauncherGroupSettings ToSettings() => new()
    {
        Name = Name,
        Collapsed = IsCollapsed,
        ShowAsList = IsListView,
        PortalPath = PortalPath,
        Items = IsPortal
            ? new()
            : Items.Select(i => new LauncherItemSettings
            {
                Label = i.Label,
                Target = i.Target,
                Arguments = i.Arguments,
                RunAsAdmin = i.RunAsAdmin,
            }).ToList(),
    };

    private void ReloadPortal()
    {
        Items.Clear();
        if (Directory.Exists(PortalPath))
        {
            try
            {
                var entries = new DirectoryInfo(PortalPath).EnumerateFileSystemInfos()
                    .Where(e => (e.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(e => e is FileInfo)    // folders first, like Explorer
                    .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);
                foreach (var entry in entries)
                    Items.Add(new LauncherItem(entry.FullName, LauncherItem.DefaultLabel(entry.FullName), isFromPortal: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Leave whatever was read; the next change event retries.
            }
        }
        OnPropertyChanged(nameof(EmptyHint));
    }

    private void StartWatching()
    {
        if (!Directory.Exists(PortalPath)) return;

        _watcher = new FileSystemWatcher(PortalPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
        };
        _watcher.Created += (_, _) => QueueReload();
        _watcher.Deleted += (_, _) => QueueReload();
        _watcher.Renamed += (_, _) => QueueReload();
        _watcher.Error += (_, _) => QueueReload();
        _watcher.EnableRaisingEvents = true;
    }

    // Watcher events arrive on thread-pool threads.
    private void QueueReload() => _dispatcher.InvokeAsync(() =>
    {
        _reloadTimer!.Stop();
        _reloadTimer.Start();
    });

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        _reloadTimer?.Stop();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
