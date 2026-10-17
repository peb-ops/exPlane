using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ExPlane.Modules.WorldClock;

/// <summary>One row of the world clock. A null zone means "follow the system's local timezone".</summary>
public sealed class ClockEntry(TimeZoneInfo? zone, string label) : INotifyPropertyChanged
{
    private string _label = label;
    private string _timeText = "";
    private string _dateText = "";
    private string _offsetText = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LabelChanged;

    public bool IsLocal => zone is null;

    public string TimeZoneId => (zone ?? TimeZoneInfo.Local).Id;

    public string Label
    {
        get => _label;
        set
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed == _label)
            {
                // Rejected or unchanged: re-raise so the TextBox snaps back to the current label.
                OnPropertyChanged();
                return;
            }
            _label = trimmed;
            OnPropertyChanged();
            LabelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string TimeText { get => _timeText; private set => SetField(ref _timeText, value); }
    public string DateText { get => _dateText; private set => SetField(ref _dateText, value); }
    public string OffsetText { get => _offsetText; private set => SetField(ref _offsetText, value); }

    public void Update(DateTime utcNow)
    {
        var target = zone ?? TimeZoneInfo.Local;
        var time = TimeZoneInfo.ConvertTimeFromUtc(utcNow, target);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);

        TimeText = time.ToString("HH:mm", CultureInfo.CurrentCulture);
        DateText = time.ToString("ddd d MMM", CultureInfo.CurrentCulture);
        OffsetText = IsLocal
            ? FormatUtcOffset(target.GetUtcOffset(utcNow))
            : FormatRelative(time - local, (time.Date - local.Date).Days);
    }

    private static string FormatUtcOffset(TimeSpan offset) =>
        $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";

    /// <summary>e.g. "+8h, tomorrow", "-5h30", "same time".</summary>
    private static string FormatRelative(TimeSpan diff, int dayDelta)
    {
        var abs = diff.Duration();
        var sign = diff < TimeSpan.Zero ? "-" : "+";
        var offset = diff == TimeSpan.Zero ? "same time"
            : abs.Minutes == 0 ? $"{sign}{(int)abs.TotalHours}h"
            : $"{sign}{(int)abs.TotalHours}h{abs.Minutes:00}";
        var day = dayDelta switch { > 0 => "tomorrow", < 0 => "yesterday", _ => null };
        return day is null ? offset : $"{offset}, {day}";
    }

    private void SetField(ref string field, string value, [CallerMemberName] string? name = null)
    {
        if (field == value) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
