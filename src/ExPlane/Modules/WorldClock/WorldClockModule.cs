using System.Windows;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

/// <summary>Footer strip: local time, then the other timezones as compact chips.</summary>
public sealed class WorldClockModule(AppSettings settings, Action save) : IPlaneModule
{
    public string Title => "World clock";

    public bool FillsSpace => false;

    public FrameworkElement View { get; } = new WorldClockView(new WorldClockViewModel(settings, save));
}
