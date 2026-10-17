using System.Windows.Controls;

namespace ExPlane.Modules.WorldClock;

/// <summary>The plane's idle "lock screen": big local time and date, then each timezone. DataContext: WorldClockViewModel.</summary>
public partial class LockScreenView : UserControl
{
    public LockScreenView() => InitializeComponent();
}
