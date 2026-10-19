using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ExPlane.Services;

/// <summary>
/// "Start with Windows" = a shortcut to the running exe in the user's Startup folder.
/// The shortcut itself is the setting, so deleting it by hand turns the option off.
/// </summary>
public static class StartupService
{
    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "exPlane.lnk");

    public static bool IsEnabled => File.Exists(ShortcutPath);

    /// <summary>Creates or removes the shortcut; false (with the reason in <paramref name="error"/>) if that failed.</summary>
    public static bool Set(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (enabled) WriteShortcut();
            else File.Delete(ShortcutPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            Debug.WriteLine($"exPlane: failed to update startup shortcut: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Re-points an existing shortcut at the current exe, in case it was moved or republished elsewhere.</summary>
    public static void Refresh()
    {
        if (IsEnabled) Set(true, out _);
    }

    private static void WriteShortcut()
    {
        var exe = Environment.ProcessPath ?? throw new IOException("Can't determine the exe path.");
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe)!);
            link.SetDescription("exPlane");
            Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
            ((IPersistFile)link).Save(ShortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    // Full vtable order up to SetPath; only the setters are used.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relPath, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
