using System.Windows;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

public sealed class WorldClockModule(AppSettings settings, Action save) : IPlaneModule
{
    public string Title => "World clock";

    public bool FillsSpace => false;

    public FrameworkElement View { get; } = new WorldClockView(new WorldClockViewModel(settings, save));
}
