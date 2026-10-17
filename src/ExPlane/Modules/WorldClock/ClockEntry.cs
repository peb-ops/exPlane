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
    private string _dayText = "";
    private string _longDateText = "";
    private string _dayMarker = "";
    private bool _isOtherDay;
    private bool _isDaytime;

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
            OnPropertyChanged(nameof(Summary));
            LabelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string TimeText { get => _timeText; private set => SetField(ref _timeText, value); }
    public string DateText { get => _dateText; private set => SetField(ref _dateText, value); }
    public string OffsetText { get => _offsetText; private set => SetField(ref _offsetText, value); }

    /// <summary>Full date for the lock view, e.g. "Saturday, 17 Oct".</summary>
    public string LongDateText { get => _longDateText; private set => SetField(ref _longDateText, value); }

    /// <summary>Short weekday, e.g. "Sat".</summary>
    public string DayText { get => _dayText; private set => SetField(ref _dayText, value); }

    /// <summary>"+1d" / "−1d" when on a different day than local time, else empty.</summary>
    public string DayMarker { get => _dayMarker; private set => SetField(ref _dayMarker, value); }

    /// <summary>Full details for tooltips, e.g. "Tokyo · Sun 18 Oct · +8h · tomorrow".</summary>
    public string Summary => $"{Label} · {DateText} · {OffsetText}";

    /// <summary>Different calendar day than local time (shown highlighted).</summary>
    public bool IsOtherDay { get => _isOtherDay; private set => SetField(ref _isOtherDay, value); }

    /// <summary>06:00–17:59 in that zone: sun icon, otherwise moon.</summary>
    public bool IsDaytime { get => _isDaytime; private set => SetField(ref _isDaytime, value); }

    public void Update(DateTime utcNow, bool use24Hour)
    {
        var target = zone ?? TimeZoneInfo.Local;
        var time = TimeZoneInfo.ConvertTimeFromUtc(utcNow, target);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        var dayDelta = (time.Date - local.Date).Days;

        TimeText = FormatTime(time, use24Hour);
        DateText = time.ToString("ddd d MMM", CultureInfo.CurrentCulture);
        OffsetText = IsLocal
            ? FormatUtcOffset(target.GetUtcOffset(utcNow))
            : FormatRelative(time - local, dayDelta);
        DayText = time.ToString("ddd", CultureInfo.CurrentCulture);
        LongDateText = time.ToString("dddd, d MMM", CultureInfo.CurrentCulture);
        IsOtherDay = !IsLocal && dayDelta != 0;
        DayMarker = IsOtherDay ? $"{(dayDelta > 0 ? "+" : "−")}{Math.Abs(dayDelta)}d" : "";
        IsDaytime = time.Hour is >= 6 and < 18;
    }

    /// <summary>"21:52" or "9:52 PM" (AM/PM in the user's language; plain AM/PM if it has none).</summary>
    private static string FormatTime(DateTime time, bool use24Hour)
    {
        var culture = CultureInfo.CurrentCulture;
        if (use24Hour) return time.ToString("HH:mm", culture);

        var morning = time.Hour < 12;
        var designator = morning ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
        if (string.IsNullOrEmpty(designator)) designator = morning ? "AM" : "PM";
        return $"{time.ToString("h:mm", culture)} {designator}";
    }

    private static string FormatUtcOffset(TimeSpan offset) =>
        $"UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";

    /// <summary>e.g. "+3h · tomorrow", "−5h30", "same time".</summary>
    private static string FormatRelative(TimeSpan diff, int dayDelta)
    {
        var abs = diff.Duration();
        var sign = diff < TimeSpan.Zero ? "−" : "+";
        var offset = diff == TimeSpan.Zero ? "same time"
            : abs.Minutes == 0 ? $"{sign}{(int)abs.TotalHours}h"
            : $"{sign}{(int)abs.TotalHours}h{abs.Minutes:00}";
        var day = dayDelta switch { > 0 => "tomorrow", < 0 => "yesterday", _ => null };
        return day is null ? offset : $"{offset} · {day}";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        if (name is nameof(DateText) or nameof(OffsetText)) OnPropertyChanged(nameof(Summary));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
