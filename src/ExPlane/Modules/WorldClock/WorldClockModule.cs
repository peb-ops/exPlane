using System.Windows;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

/// <summary>Footer strip (local time, then the other timezones as compact chips), plus the idle lock view.</summary>
public sealed class WorldClockModule : IPlaneModule
{
    public WorldClockModule(AppSettings settings, Action save)
    {
        var viewModel = new WorldClockViewModel(settings, save);
        View = new WorldClockView(viewModel);
        LockView = new LockScreenView { DataContext = viewModel };
    }

    public string Title => "World clock";

    public bool FillsSpace => false;

    public FrameworkElement View { get; }

    /// <summary>Big time, date and timezone rows, shown by the plane while idle.</summary>
    public FrameworkElement LockView { get; }
}
