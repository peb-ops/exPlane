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

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public ImageSource? Icon => _icon ??= ShellIcons.For(this);

    public void Launch(bool asAdmin)
    {
        var info = new ProcessStartInfo(Target) { UseShellExecute = true, Arguments = Arguments ?? "" };
        if (IsPath && File.Exists(Target)) info.WorkingDirectory = Path.GetDirectoryName(Target)!;
        if (asAdmin) info.Verb = "runas";
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
