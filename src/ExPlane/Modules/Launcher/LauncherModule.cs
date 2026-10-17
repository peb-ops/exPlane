using System.Windows;
using ExPlane.Services;

namespace ExPlane.Modules.Launcher;

public sealed class LauncherModule(AppSettings settings, Action save) : IPlaneModule
{
    public string Title => "Launcher";

    public bool FillsSpace => true;

    public FrameworkElement View { get; } = new LauncherView(new LauncherViewModel(settings, save));
}
