using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ExPlane.Modules.Launcher;

/// <summary>Reads a web link dragged from a browser (address bar, tab or link) out of drag-and-drop data.</summary>
internal static class DroppedLink
{
    public static bool TryRead(IDataObject data, out string url, out string? title)
    {
        url = "";
        title = null;

        // Firefox: "url\ntitle"
        if (ReadText(data, "text/x-moz-url") is { } moz)
        {
            var lines = moz.Split('\n');
            if (IsWebUrl(lines[0].Trim()))
            {
                url = lines[0].Trim();
                title = lines.Length > 1 ? lines[1].Trim() : null;
                return true;
            }
        }

        // Chrome / Edge: the URL, plus a "<page title>.url" virtual file name.
        var candidate = (ReadText(data, "UniformResourceLocatorW") ?? ReadText(data, DataFormats.UnicodeText))?.Trim();
        if (candidate is null || !IsWebUrl(candidate)) return false;

        url = candidate;
        title = ReadDescriptorTitle(data);
        return true;
    }

    public static bool IsWebUrl(string text) =>
        !text.Contains('\n')
        && Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? ReadText(IDataObject data, string format)
    {
        try
        {
            if (!data.GetDataPresent(format)) return null;
            return data.GetData(format) switch
            {
                string text => text,
                MemoryStream stream => Encoding.Unicode.GetString(stream.ToArray()).TrimEnd('\0'),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Another app's drag data; some formats can't be rendered on request.
            return null;
        }
    }

    /// <summary>The page title from FILEGROUPDESCRIPTORW: the first descriptor's cFileName, minus ".url".</summary>
    private static string? ReadDescriptorTitle(IDataObject data)
    {
        try
        {
            if (data.GetData("FileGroupDescriptorW") is not MemoryStream stream) return null;
            var bytes = stream.ToArray();

            // DWORD cItems, then FILEDESCRIPTORW: 72 bytes of fields before cFileName[260] (WCHAR).
            const int nameOffset = 4 + 72;
            if (bytes.Length <= nameOffset) return null;
            var name = Encoding.Unicode.GetString(bytes, nameOffset, Math.Min(520, bytes.Length - nameOffset)).Split('\0')[0];
            return name.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && name.Length > 4 ? name[..^4] : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return null;
        }
    }
}
