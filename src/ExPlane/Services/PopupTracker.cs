using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExPlane.Services;

/// <summary>Whether any of this app's popups (context menus, submenus, tooltips) is currently on screen.</summary>
public static class PopupTracker
{
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // WPF hosts every Popup in its own HwndSource whose root visual is the (internal) PopupRoot type.
    public static bool AnyOpen() =>
        PresentationSource.CurrentSources.OfType<HwndSource>().Any(source =>
            !source.IsDisposed
            && source.RootVisual?.GetType().Name == "PopupRoot"
            && IsWindowVisible(source.Handle));
}
