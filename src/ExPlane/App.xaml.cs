using System.Windows;
using ExPlane.Services;

namespace ExPlane;

public partial class App : Application
{
    private const string ShowSignalName = "exPlane.ShowSignal";
    private const string ExitSignalName = "exPlane.ExitSignal";

    private EventWaitHandle? _showSignal;
    private EventWaitHandle? _exitSignal;
    private SettingsService _settingsService = null!;
    private AppSettings _settings = null!;
    private TopmostService _topmost = null!;
    private MainWindow _main = null!;
    private TrayService? _tray;
    private HotkeyService? _hotkey;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // "exPlane.exe --exit" asks a running instance to quit cleanly (used by terminate.bat).
        if (e.Args.Contains("--exit", StringComparer.OrdinalIgnoreCase))
        {
            if (EventWaitHandle.TryOpenExisting(ExitSignalName, out var exitSignal))
            {
                exitSignal.Set();
                exitSignal.Dispose();
            }
            Shutdown();
            return;
        }

        // Single instance: a second launch just asks the running one to show itself.
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName, out var createdNew);
        if (!createdNew)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.InvokeAsync(ShowMain), null, Timeout.Infinite, false);
        _exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ExitSignalName);
        ThreadPool.RegisterWaitForSingleObject(_exitSignal, (_, _) => Dispatcher.InvokeAsync(ExitApp), null, Timeout.Infinite, true);

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _topmost = new TopmostService(_settings.Topmost);
        _main = new MainWindow(_settings, () => _settingsService.Save(_settings), _topmost);

        _tray = new TrayService(_topmost.IsPinned, _settings.Hotkey);
        _tray.ShowHideRequested += (_, _) => { if (_main.IsVisible) _main.HideToTray(); else ShowMain(); };
        _tray.TogglePinRequested += (_, _) => _topmost.Toggle();
        _tray.ExitRequested += (_, _) => ExitApp();

        _topmost.Changed += (_, _) =>
        {
            _tray.SetPinned(_topmost.IsPinned);
            _settings.Topmost = _topmost.IsPinned;
            _settingsService.Save(_settings);
        };

        _hotkey = new HotkeyService();
        _hotkey.Pressed += (_, _) => OnHotkey();
        if (!_hotkey.Register(_main, _settings.Hotkey))
            _tray.ShowBalloon("exPlane", $"Couldn't register hotkey {_settings.Hotkey}. Use the pin button or tray menu instead.");

        ShowMain();
    }

    private void OnHotkey()
    {
        // From hidden, the hotkey brings the plane back pinned; otherwise it toggles.
        if (!_main.IsVisible)
        {
            ShowMain();
            _topmost.Set(true);
        }
        else
        {
            _topmost.Toggle();
        }
    }

    private void ShowMain()
    {
        _main.Show();
        _main.Activate();
    }

    private void ExitApp()
    {
        _main.PrepareForExit();
        _settingsService.Save(_settings);
        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _main.PrepareForExit();
        _settingsService.Save(_settings);
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        _showSignal?.Dispose();
        _exitSignal?.Dispose();
        base.OnExit(e);
    }
}
