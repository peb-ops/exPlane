using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExPlane.Modules.Launcher;

/// <summary>
/// Site icons for web links, fetched directly from each site (no third-party service) and cached under
/// %LOCALAPPDATA%\exPlane\favicons. Never throws; null means "keep the generic browser icon".
/// </summary>
internal static partial class Favicons
{
    private const int MaxDownloadBytes = 1024 * 1024;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan RetryFailedAfter = TimeSpan.FromDays(3);

    private static readonly HttpClient Http = CreateClient();

    // One lookup per host per run; completed tasks double as an in-memory cache.
    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Lookups = new(StringComparer.OrdinalIgnoreCase);

    private static string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "exPlane", "favicons");

    public static Task<ImageSource?> GetAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Task.FromResult<ImageSource?>(null);

        return Lookups.GetOrAdd(uri.Host, _ => Task.Run(() => LoadAsync(uri)));
    }

    private static async Task<ImageSource?> LoadAsync(Uri uri)
    {
        var name = string.Concat(uri.Host.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var iconFile = Path.Combine(CacheDir, name + ".png");
        var failedMarker = Path.Combine(CacheDir, name + ".none");

        try
        {
            if (IsFresh(iconFile, MaxAge)) return Decode(await File.ReadAllBytesAsync(iconFile));
            if (IsFresh(failedMarker, RetryFailedAfter)) return null;

            var icon = await DownloadAsync(uri);
            Directory.CreateDirectory(CacheDir);
            if (icon is null)
            {
                await File.WriteAllTextAsync(failedMarker, "");
                return File.Exists(iconFile) ? Decode(await File.ReadAllBytesAsync(iconFile)) : null;
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(icon));
            using (var file = File.Create(iconFile)) encoder.Save(file);
            File.Delete(failedMarker);
            return icon;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsFresh(string path, TimeSpan maxAge) =>
        File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < maxAge;

    /// <summary>Icons the home page declares (largest first), then /favicon.ico.</summary>
    private static async Task<BitmapSource?> DownloadAsync(Uri uri)
    {
        var site = new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
        foreach (var candidate in await FindDeclaredIconsAsync(site))
        {
            if (Decode(await TryGetBytesAsync(candidate)) is { } icon) return icon;
        }
        return Decode(await TryGetBytesAsync(new Uri(site, "/favicon.ico")));
    }

    private static async Task<List<Uri>> FindDeclaredIconsAsync(Uri site)
    {
        var icons = new List<(Uri Uri, int Rank)>();
        try
        {
            using var response = await Http.GetAsync(site, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return new();
            var html = await ReadLimitedAsync(response, 512 * 1024) is { } bytes
                ? System.Text.Encoding.UTF8.GetString(bytes)
                : "";
            var pageUri = response.RequestMessage?.RequestUri ?? site;

            foreach (Match link in LinkTag().Matches(html))
            {
                var attributes = Attribute().Matches(link.Value)
                    .GroupBy(a => a.Groups["name"].Value.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups["value"].Value));
                if (!attributes.TryGetValue("rel", out var rel) || !rel.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;
                if (!attributes.TryGetValue("href", out var href) || href.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                if (href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue; // WPF can't decode SVG
                if (!Uri.TryCreate(pageUri, href, out var iconUri)) continue;

                // Prefer apple-touch-icon (usually 180px), then the largest declared size.
                var size = attributes.TryGetValue("sizes", out var sizes) && int.TryParse(sizes.Split('x')[0], out var s) ? s : 16;
                icons.Add((iconUri, rel.Contains("apple", StringComparison.OrdinalIgnoreCase) ? 1000 + size : size));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
        }
        return icons.OrderByDescending(i => i.Rank).Select(i => i.Uri).Distinct().Take(4).ToList();
    }

    private static async Task<byte[]?> TryGetBytesAsync(Uri uri)
    {
        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode ? await ReadLimitedAsync(response, MaxDownloadBytes) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, int limit)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit) break; // enough for an icon or a page head
        }
        return buffer.Length > 0 ? buffer.ToArray() : null;
    }

    /// <summary>PNG/ICO/etc. to a frozen bitmap (largest frame up to 256px), or null if it isn't an image.</summary>
    private static BitmapSource? Decode(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                                               BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth <= 256 ? f.PixelWidth : 0).First();
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException
                                       or InvalidOperationException or OverflowException or IOException)
        {
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        // Some sites refuse requests without a browser-like user agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) exPlane/1.0");
        return client;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"\b(?<name>rel|href|sizes)\s*=\s*([""'])(?<value>.*?)\1", RegexOptions.IgnoreCase)]
    private static partial Regex Attribute();
}
