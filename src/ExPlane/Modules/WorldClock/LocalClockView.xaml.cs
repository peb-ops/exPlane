using System.Windows.Controls;

namespace ExPlane.Modules.WorldClock;

/// <summary>Local time + weekday, leading the clock footer. DataContext: the local ClockEntry.</summary>
public partial class LocalClockView : UserControl
{
    public LocalClockView() => InitializeComponent();
}
