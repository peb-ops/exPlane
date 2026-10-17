using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExPlane.Modules.Launcher;

/// <summary>Icons as Explorer shows them (shortcut targets, folders, file types), via SHGetFileInfo. Cached per path.</summary>
internal static class ShellIcons
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? For(LauncherItem item)
    {
        if (item.IsUrl) return ForType(".html");          // default browser's icon
        if (!item.IsPath) return ForType(".exe");         // bare command, e.g. "notepad"
        if (File.Exists(item.Target) || Directory.Exists(item.Target)) return ForPath(item.Target);
        var extension = Path.GetExtension(item.Target);
        return ForType(extension.Length > 0 ? extension : ".exe");
    }

    private static ImageSource? ForPath(string path) => Get(path, path, 0, SHGFI_ICON | SHGFI_LARGEICON);

    /// <summary>Generic icon for a file type without touching the disk.</summary>
    private static ImageSource? ForType(string extension) =>
        Get("type:" + extension, extension, FILE_ATTRIBUTE_NORMAL, SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

    private static ImageSource? Get(string key, string path, uint attributes, uint flags)
    {
        if (Cache.TryGetValue(key, out var cached)) return cached;

        ImageSource? image = null;
        var info = new SHFILEINFO();
        if (SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) != IntPtr.Zero
            && info.hIcon != IntPtr.Zero)
        {
            try
            {
                var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                image = bitmap;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        Cache[key] = image;
        return image;
    }
}
