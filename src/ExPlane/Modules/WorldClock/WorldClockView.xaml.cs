using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ClockEntry entry)
            _viewModel.Remove(entry);
    }

    private void Label_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;

        var binding = ((TextBox)sender).GetBindingExpression(TextBox.TextProperty);
        if (e.Key == Key.Enter) binding?.UpdateSource();
        else binding?.UpdateTarget();
        Keyboard.ClearFocus();
        e.Handled = true;
    }
}
