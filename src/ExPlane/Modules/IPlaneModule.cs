using System.Windows;

namespace ExPlane.Modules;

/// <summary>A tool hosted on the plane. Add new tools by implementing this and listing them in MainWindow.</summary>
public interface IPlaneModule
{
    string Title { get; }
    FrameworkElement View { get; }

    /// <summary>True: takes the remaining height of the plane. False: sized to its content.</summary>
    bool FillsSpace { get; }
}
