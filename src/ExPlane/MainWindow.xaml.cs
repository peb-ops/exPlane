using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ExPlane.Modules;
using ExPlane.Modules.Launcher;
using ExPlane.Modules.WorldClock;
using ExPlane.Services;

namespace ExPlane;

public partial class MainWindow : Window
{
    private const double ScreenMargin = 24;
    private const double MaxContentShare = 0.6;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(200);

    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _isLocked;

    private readonly List<(Border Container, IPlaneModule Module)> _modules = new();
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly TopmostService _topmost;
    private bool _allowClose;

    /// <summary>"Exit exPlane" in the ⋯ menu; the app decides how to shut down.</summary>
    public event EventHandler? ExitRequested;

    public MainWindow(AppSettings settings, Action save, TopmostService topmost)
    {
        InitializeComponent();
        WindowCorners.Round(this);
        _settings = settings;
        _save = save;
        _topmost = topmost;

        // Top to bottom: the launcher fills the plane, the clocks sit underneath as a footer.
        var clocks = new WorldClockModule(settings, save);
        HostModules(new IPlaneModule[]
        {
            new LauncherModule(settings, save),
            clocks,
        });
        LockLayer.Child = clocks.LockView;

        ApplyBounds(settings.Window);
        ApplyPinned();
        _topmost.Changed += (_, _) => ApplyPinned();

        // Anything done to the plane counts as use and postpones the lock view.
        PreviewMouseMove += (_, _) => NoteActivity();
        PreviewMouseDown += (_, _) => NoteActivity();
        PreviewMouseWheel += (_, _) => NoteActivity();
        PreviewKeyDown += (_, _) => NoteActivity();
        PreviewDragOver += (_, _) => NoteActivity();
        IsVisibleChanged += (_, _) =>
        {
            // Coming back from the tray / hotkey always shows the main view.
            if (!IsVisible) return;
            SetLocked(false, animate: false);
            NoteActivity();
        };
        _idleTimer.Tick += (_, _) => CheckIdle();
        _idleTimer.Start();
    }

    private void NoteActivity() => _lastActivity = DateTime.UtcNow;

    private void CheckIdle()
    {
        if (_isLocked || !IsVisible || _settings.LockAfterSeconds <= 0) return;

        // The pointer resting on the plane, an open dialog/menu, or a rename in progress all mean "in use".
        var busy = IsMouseOver
                   || OwnedWindows.Count > 0
                   || PopupTracker.AnyOpen()
                   || (Keyboard.FocusedElement is TextBox box && IsAncestorOf(box));
        if (busy)
        {
            NoteActivity();
            return;
        }

        if (DateTime.UtcNow - _lastActivity >= TimeSpan.FromSeconds(_settings.LockAfterSeconds))
            SetLocked(true, animate: true);
    }

    /// <summary>Cross-fades between the main view and the lock view.</summary>
    private void SetLocked(bool locked, bool animate)
    {
        if (locked == _isLocked) return;
        _isLocked = locked;
        var duration = animate ? FadeDuration : TimeSpan.Zero;

        if (locked)
        {
            LockLayer.Visibility = Visibility.Visible;
            Fade(LockLayer, 1, duration);
            Fade(ModuleHost, 0, duration, () =>
            {
                if (_isLocked) ModuleHost.Visibility = Visibility.Hidden; // no clicks reach the launcher behind
            });
        }
        else
        {
            ModuleHost.Visibility = Visibility.Visible;
            Fade(ModuleHost, 1, duration);
            Fade(LockLayer, 0, duration, () =>
            {
                if (!_isLocked) LockLayer.Visibility = Visibility.Collapsed;
            });
        }
    }

    private static void Fade(UIElement element, double to, TimeSpan duration, Action? completed = null)
    {
        if (duration == TimeSpan.Zero)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = to;
            completed?.Invoke();
            return;
        }

        var animation = new DoubleAnimation(to, duration) { EasingFunction = new QuadraticEase() };
        if (completed is not null) animation.Completed += (_, _) => completed();
        element.BeginAnimation(OpacityProperty, animation);
    }

    private void LockLayer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLocked) return;
        SetLocked(false, animate: true);
        NoteActivity();
        e.Handled = true; // the unlocking click must not also act on the launcher
    }

    public void HideToTray()
    {
        SetLocked(false, animate: false);
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
                BorderBrush = (Brush)FindResource("DividerBrush"),
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

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.Placement = PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void MoreMenu_Opened(object sender, RoutedEventArgs e)
    {
        TopmostMenuItem.IsChecked = _topmost.IsPinned;
        TopmostMenuItem.InputGestureText = _settings.Hotkey;
        foreach (var option in LockAfterMenu.Items.OfType<MenuItem>())
            option.IsChecked = Equals(option.Tag, _settings.LockAfterSeconds.ToString());
    }

    private void LockNowMenuItem_Click(object sender, RoutedEventArgs e) => SetLocked(true, animate: true);

    private void LockAfterMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } || !int.TryParse(tag, out var seconds)) return;
        _settings.LockAfterSeconds = seconds;
        _save();
        NoteActivity();
    }

    private void TopmostMenuItem_Click(object sender, RoutedEventArgs e) => _topmost.Toggle();

    private void HideMenuItem_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
