using System.Windows;
using System.Windows.Controls;
using ExPlane.Controls;

namespace ExPlane.Modules.WorldClock;

public partial class WorldClockView : UserControl
{
    private readonly WorldClockViewModel _viewModel;

    public WorldClockView(WorldClockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddTimezoneDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.SelectedZone is { } zone)
            _viewModel.Add(zone, dialog.ChosenLabel);
    }

    private void RenameMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ClockEntry entry) return;

        var label = PromptDialog.Ask(Window.GetWindow(this), "Rename clock", "Label", entry.Label);
        if (label is not null) entry.Label = label;
    }

    /// <summary>Shows the current 12/24-hour choice as a check on the "24-hour clock" item.</summary>
    private void ClockMenu_Opened(object sender, RoutedEventArgs e)
    {
        foreach (var item in ((ContextMenu)sender).Items.OfType<MenuItem>())
        {
            if (Equals(item.Tag, "24h")) item.IsChecked = _viewModel.Use24Hour;
        }
    }

    private void Use24Hour_Click(object sender, RoutedEventArgs e) => _viewModel.Use24Hour = !_viewModel.Use24Hour;

    private void RemoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ClockEntry entry)
            _viewModel.Remove(entry);
    }
}
