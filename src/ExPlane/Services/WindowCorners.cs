using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExPlane.Services;

/// <summary>Asks DWM for Windows 11 rounded corners on borderless (WindowChrome) windows. No-op on Windows 10.</summary>
public static class WindowCorners
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Round(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        };
    }
}
