using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ExPlane.Modules.Launcher;

/// <summary>Something launchable: a file, folder, URL or command, opened through the Windows shell.</summary>
public sealed class LauncherItem(string target, string label, string? arguments = null, bool isFromPortal = false)
    : INotifyPropertyChanged
{
    private string _target = target;
    private string _label = label;
    private string? _arguments = arguments;
    private ImageSource? _icon;
    private bool _showInsertMarker;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Always launch elevated (still prompts UAC).</summary>
    public bool RunAsAdmin { get; set; }

    /// <summary>Drag feedback: a dropped item would land just before this one.</summary>
    public bool ShowInsertMarker
    {
        get => _showInsertMarker;
        set
        {
            if (value == _showInsertMarker) return;
            _showInsertMarker = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Items mirrored from a portal folder can be launched but not edited or moved.</summary>
    public bool IsFromPortal => isFromPortal;

    public bool CanEdit => !isFromPortal;

    public string Target
    {
        get => _target;
        set
        {
            if (value == _target) return;
            _target = value;
            _icon = null;
            Refresh();
        }
    }

    public string Label
    {
        get => _label;
        set
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed == _label) return;
            _label = trimmed;
            OnPropertyChanged();
        }
    }

    public string? Arguments
    {
        get => _arguments;
        set
        {
            _arguments = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    public bool IsUrl => Uri.TryCreate(Target, UriKind.Absolute, out var uri) && !uri.IsFile;

    /// <summary>An http(s) link (as opposed to mailto:, ms-settings: and other URL schemes).</summary>
    public bool IsWebUrl => DroppedLink.IsWebUrl(Target);

    /// <summary>Secondary text in list view: the site for web links ("github.com"), else empty.</summary>
    public string Detail => IsWebUrl && Uri.TryCreate(Target, UriKind.Absolute, out var uri)
        ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host)
        : "";

    /// <summary>A full file-system path (as opposed to a URL or a bare command like "notepad").</summary>
    public bool IsPath => !IsUrl && Path.IsPathFullyQualified(Target);

    public bool IsMissing => IsPath && !File.Exists(Target) && !Directory.Exists(Target);

    public string ToolTipText
    {
        get
        {
            var text = Arguments is null ? Target : $"{Target} {Arguments}";
            return IsMissing ? text + "\n(not found)" : text;
        }
    }

    /// <summary>Shell icon right away; for web links the site's icon replaces it once fetched (in the background).</summary>
    public ImageSource? Icon
    {
        get
        {
            if (_icon is null)
            {
                _icon = ShellIcons.For(this);
                if (IsWebUrl) _ = LoadSiteIconAsync();
            }
            return _icon;
        }
    }

    private async Task LoadSiteIconAsync()
    {
        var target = Target;
        var siteIcon = await Favicons.GetAsync(target);
        if (siteIcon is null || target != Target) return; // edited meanwhile: that target loads its own
        _icon = siteIcon;
        OnPropertyChanged(nameof(Icon));
    }

    public void Launch(bool asAdmin)
    {
        var info = new ProcessStartInfo(Target) { UseShellExecute = true, Arguments = Arguments ?? "" };
        if (IsPath && File.Exists(Target)) info.WorkingDirectory = Path.GetDirectoryName(Target)!;
        if (asAdmin || RunAsAdmin) info.Verb = "runas";
        Process.Start(info)?.Dispose();
    }

    public void OpenLocation() => Process.Start("explorer.exe", $"/select,\"{Target}\"")?.Dispose();

    /// <summary>Re-evaluates everything derived from the target (missing state, tooltip, icon).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    /// <summary>"C:\Tools\app.exe" -> "app", "https://github.com/x" -> "github.com", "D:\Work" -> "Work".</summary>
    public static string DefaultLabel(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
            return string.IsNullOrEmpty(uri.Host) ? target : uri.Host;

        var name = Path.GetFileName(target.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(name)) return target;

        var extension = Path.GetExtension(name);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
