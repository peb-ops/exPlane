using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace ExPlane.Modules.Launcher;

public partial class LauncherView : UserControl
{
    private const string ItemFormat = "exPlane.LauncherItem";
    private const int ErrorCancelled = 1223; // user dismissed the UAC prompt

    private readonly LauncherViewModel _viewModel;
    private LauncherItem? _pressedItem;
    private Point _pressPoint;

    public LauncherView(LauncherViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    private static T? ContextOf<T>(object sender) where T : class => (sender as FrameworkElement)?.DataContext as T;

    // ---- Items: click to launch, drag to reorder ----

    private void Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedItem = ContextOf<LauncherItem>(sender);
        _pressPoint = e.GetPosition(this);
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedItem is null || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        // Past the threshold it's a drag, not a click.
        var item = _pressedItem;
        _pressedItem = null;
        if (item.IsFromPortal) return;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ItemFormat, item), DragDropEffects.Move);
    }

    private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = ContextOf<LauncherItem>(sender);
        if (item is not null && ReferenceEquals(item, _pressedItem)) TryLaunch(item, asAdmin: false);
        _pressedItem = null;
    }

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherItem>(sender) is { } item) TryLaunch(item, asAdmin: false);
    }

    private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherItem>(sender) is { } item) TryLaunch(item, asAdmin: true);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e) => ContextOf<LauncherItem>(sender)?.OpenLocation();

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherItem>(sender) is not { } item) return;

        var dialog = new EditItemDialog(item) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        item.Target = dialog.Target;
        item.Arguments = dialog.Arguments;
        item.Label = dialog.ItemLabel;
        _viewModel.Persist();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherItem>(sender) is { } item) _viewModel.RemoveItem(item);
    }

    private void TryLaunch(LauncherItem item, bool asAdmin)
    {
        try
        {
            item.Launch(asAdmin);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            item.Refresh();
            MessageBox.Show(Window.GetWindow(this), $"Couldn't open \"{item.Label}\".\n\n{ex.Message}", "exPlane",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- Drag & drop onto groups (files from Explorer, or items being moved) ----

    private void Group_DragOver(object sender, DragEventArgs e)
    {
        var group = ContextOf<LauncherGroup>(sender);
        e.Effects = group is null || group.IsPortal ? DragDropEffects.None
            : e.Data.GetDataPresent(ItemFormat) ? DragDropEffects.Move
            : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Group_Drop(object sender, DragEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is not { IsPortal: false } group) return;

        var index = DropIndex(e, group);
        if (e.Data.GetData(ItemFormat) is LauncherItem item) _viewModel.MoveItem(item, group, index);
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) _viewModel.AddPaths(group, paths, index);
        e.Handled = true;
    }

    /// <summary>Index of the item under the cursor (insert before it), or -1 to append.</summary>
    private static int DropIndex(DragEventArgs e, LauncherGroup group)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null;
             node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { DataContext: LauncherItem item } && group.Items.Contains(item))
                return group.Items.IndexOf(item);
        }
        return -1;
    }

    // ---- Groups ----

    private void GroupHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The name TextBox handles its own clicks, so this only fires on the rest of the header.
        if (ContextOf<LauncherGroup>(sender) is { } group) group.IsCollapsed = !group.IsCollapsed;
    }

    private void GroupName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;

        var binding = ((TextBox)sender).GetBindingExpression(TextBox.TextProperty);
        if (e.Key == Key.Enter) binding?.UpdateSource();
        else binding?.UpdateTarget();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is not { } group) return;

        var dialog = new EditItemDialog(null) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            _viewModel.AddItem(group, new LauncherItem(dialog.Target, dialog.ItemLabel, dialog.Arguments));
    }

    private void OpenPortalFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is { PortalPath: { } path } && Directory.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
    }

    private void MoveGroupUp_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is { } group) _viewModel.MoveGroup(group, -1);
    }

    private void MoveGroupDown_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is { } group) _viewModel.MoveGroup(group, +1);
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (ContextOf<LauncherGroup>(sender) is not { } group) return;

        if (!group.IsPortal && !group.IsEmpty)
        {
            var answer = MessageBox.Show(Window.GetWindow(this),
                $"Delete group \"{group.Name}\" and its {group.Items.Count} item(s)?\nThe files themselves are not touched.",
                "exPlane", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }
        _viewModel.RemoveGroup(group);
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e) => _viewModel.AddGroup();

    private void AddPortal_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to show in the plane" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) _viewModel.AddPortalGroup(dialog.FolderName);
    }
}
