using System.Collections.ObjectModel;
using System.Windows.Threading;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

public sealed class WorldClockViewModel
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly DispatcherTimer _timer;

    public ObservableCollection<ClockEntry> Entries { get; } = new();

    public WorldClockViewModel(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;

        Entries.Add(new ClockEntry(null, "Local"));
        foreach (var clock in settings.Clocks)
        {
            // Skip ids this machine doesn't know rather than failing startup.
            if (TimeZoneInfo.TryFindSystemTimeZoneById(clock.TimeZoneId, out var zone))
                Entries.Add(CreateEntry(zone, clock.Label));
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    public void Add(TimeZoneInfo zone, string label)
    {
        var entry = CreateEntry(zone, label);
        entry.Update(DateTime.UtcNow);
        Entries.Add(entry);
        Persist();
    }

    public void Remove(ClockEntry entry)
    {
        if (entry.IsLocal) return;
        Entries.Remove(entry);
        Persist();
    }

    private ClockEntry CreateEntry(TimeZoneInfo zone, string label)
    {
        var entry = new ClockEntry(zone, label);
        entry.LabelChanged += (_, _) => Persist();
        return entry;
    }

    private void Refresh()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in Entries) entry.Update(now);
    }

    private void Persist()
    {
        _settings.Clocks = Entries
            .Where(e => !e.IsLocal)
            .Select(e => new ClockSettings { TimeZoneId = e.TimeZoneId, Label = e.Label })
            .ToList();
        _save();
    }
}
