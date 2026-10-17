using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

public sealed class WorldClockViewModel
{
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly DispatcherTimer _timer;

    /// <summary>The system's own timezone, shown large above the list.</summary>
    public ClockEntry Local { get; } = new(null, "Local");

    /// <summary>The user's added timezones.</summary>
    public ObservableCollection<ClockEntry> Entries { get; } = new();

    public WorldClockViewModel(AppSettings settings, Action save)
    {
        _settings = settings;
        _save = save;

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

    /// <summary>24-hour vs 12-hour times; follows the Windows time format until set.</summary>
    public bool Use24Hour
    {
        get => _settings.Use24HourClock ?? WindowsUses24Hour;
        set
        {
            _settings.Use24HourClock = value;
            _save();
            Refresh();
        }
    }

    // "H" (0-23) in the short time pattern means a 24-hour format, e.g. "HH:mm" vs "h:mm tt".
    private static bool WindowsUses24Hour => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');

    public void Add(TimeZoneInfo zone, string label)
    {
        var entry = CreateEntry(zone, label);
        entry.Update(DateTime.UtcNow, Use24Hour);
        Entries.Add(entry);
        Persist();
    }

    public void Remove(ClockEntry entry)
    {
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
        var use24Hour = Use24Hour;
        Local.Update(now, use24Hour);
        foreach (var entry in Entries) entry.Update(now, use24Hour);
    }

    private void Persist()
    {
        _settings.Clocks = Entries
            .Select(e => new ClockSettings { TimeZoneId = e.TimeZoneId, Label = e.Label })
            .ToList();
        _save();
    }
}
