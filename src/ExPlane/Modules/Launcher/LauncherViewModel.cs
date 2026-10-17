using System.Collections.ObjectModel;
using ExPlane.Services;

namespace ExPlane.Modules.Launcher;

public sealed class LauncherViewModel
{
    private readonly AppSettings _settings;
    private readonly Action _save;

    public ObservableCollection<LauncherGroup> Groups { get; } = new();

    public LauncherViewModel(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;

        foreach (var saved in settings.Launcher.Groups)
        {
            var group = new LauncherGroup(saved.Name, saved.Collapsed, saved.PortalPath, Persist);
            if (!group.IsPortal)
            {
                foreach (var item in saved.Items)
                    group.Items.Add(new LauncherItem(item.Target, item.Label, item.Arguments) { RunAsAdmin = item.RunAsAdmin });
            }
            Groups.Add(group);
        }

        // Something to drop onto on first run.
        if (Groups.Count == 0) Groups.Add(new LauncherGroup("Apps", false, null, Persist));
    }

    public void AddGroup()
    {
        Groups.Add(new LauncherGroup("New group", false, null, Persist));
        Persist();
    }

    public void AddPortalGroup(string folder)
    {
        Groups.Add(new LauncherGroup(LauncherItem.DefaultLabel(folder), false, folder, Persist));
        Persist();
    }

    public void RemoveGroup(LauncherGroup group)
    {
        Groups.Remove(group);
        group.Dispose();
        Persist();
    }

    public void MoveGroup(LauncherGroup group, int delta)
    {
        var from = Groups.IndexOf(group);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Groups.Count) return;
        Groups.Move(from, to);
        Persist();
    }

    /// <summary>Adds dropped files/folders at <paramref name="index"/> (-1 = end).</summary>
    public void AddPaths(LauncherGroup group, IEnumerable<string> paths, int index)
    {
        foreach (var path in paths)
        {
            var item = new LauncherItem(path, LauncherItem.DefaultLabel(path));
            if (index < 0 || index >= group.Items.Count) group.Items.Add(item);
            else group.Items.Insert(index++, item);
        }
        Persist();
    }

    public void AddItem(LauncherGroup group, LauncherItem item)
    {
        group.Items.Add(item);
        Persist();
    }

    public void RemoveItem(LauncherItem item)
    {
        GroupOf(item)?.Items.Remove(item);
        Persist();
    }

    /// <summary>Moves an item within or between groups, inserting before <paramref name="index"/> (-1 = end).</summary>
    public void MoveItem(LauncherItem item, LauncherGroup target, int index)
    {
        var source = GroupOf(item);
        if (source is null || target.IsPortal) return;

        var from = source.Items.IndexOf(item);
        source.Items.RemoveAt(from);
        if (ReferenceEquals(source, target) && index > from) index--;
        if (index < 0 || index > target.Items.Count) index = target.Items.Count;
        target.Items.Insert(index, item);
        Persist();
    }

    public LauncherGroup? GroupOf(LauncherItem item) => Groups.FirstOrDefault(g => g.Items.Contains(item));

    public void Persist()
    {
        _settings.Launcher.Groups = Groups.Select(g => g.ToSettings()).ToList();
        _save();
    }
}
