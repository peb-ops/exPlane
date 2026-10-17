using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExPlane.Modules;
using ExPlane.Modules.Launcher;
using ExPlane.Modules.WorldClock;
using ExPlane.Services;

namespace ExPlane;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;
    private const double MaxContentShare = 0.6;

    private readonly List<(Border Container, IPlaneModule Module)> _modules = new();
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly TopmostService _topmost;
    private bool _allowClose;

    public MainWindow(AppSettings settings, Action save, TopmostService topmost)
    {
        InitializeComponent();
        _settings = settings;
        _save = save;
        _topmost = topmost;

        HostModules(new IPlaneModule[]
        {
            new WorldClockModule(settings, save),
            new LauncherModule(settings, save),
        });

        ApplyBounds(settings.Window);
        ApplyPinned();
        _topmost.Changed += (_, _) => ApplyPinned();
    }

    public void HideToTray()
    {
        CaptureBounds();
        _save();
        Hide();
    }

    public void PrepareForExit()
    {
        CaptureBounds();
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        // No maximize (caption double-click) and no minimize (Win+Down): the tray is the "minimized" state.
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            HideToTray();
        }
        else if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        base.OnStateChanged(e);
    }

    private void HostModules(IEnumerable<IPlaneModule> modules)
    {
        foreach (var module in modules)
        {
            var container = new Border
            {
                Child = module.View,
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(0, _modules.Count == 0 ? 0 : 1, 0, 0),
            };
            ModuleHost.RowDefinitions.Add(new RowDefinition
            {
                Height = module.FillsSpace ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            });
            Grid.SetRow(container, _modules.Count);
            ModuleHost.Children.Add(container);
            _modules.Add((container, module));
        }
    }

    private void ModuleHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Content-sized modules get at most 60% when a filling module shares the plane, so it never gets squeezed out.
        if (!_modules.Any(m => m.Module.FillsSpace)) return;
        foreach (var (container, module) in _modules)
        {
            if (!module.FillsSpace) container.MaxHeight = e.NewSize.Height * MaxContentShare;
        }
    }

    private void ApplyPinned()
    {
        var pinned = _topmost.IsPinned;
        Topmost = pinned;
        PinButton.IsChecked = pinned;
        PinGlyph.Text = pinned ? "" : "";
        PinButton.ToolTip = pinned ? $"Unpin ({_settings.Hotkey})" : $"Keep on top ({_settings.Hotkey})";
    }

    private void ApplyBounds(WindowSettings w)
    {
        if (w.Width is double width) Width = width;
        if (w.Height is double height) Height = height;

        // Only restore the saved position if the title bar would still be on a screen.
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                              SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (w.Left is double left && w.Top is double top && screen.Contains(new Point(left + 40, top + 15)))
        {
            Left = left;
            Top = top;
        }
        else
        {
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - ScreenMargin;
            Top = work.Top + ScreenMargin;
        }
    }

    private void CaptureBounds()
    {
        if (ActualWidth <= 0) return;
        _settings.Window = new WindowSettings
        {
            Left = Left,
            Top = Top,
            Width = ActualWidth,
            Height = ActualHeight,
        };
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => _topmost.Toggle();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();
}
