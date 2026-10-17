using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExPlane.Services;
using Microsoft.Win32;

namespace ExPlane.Modules.Launcher;

/// <summary>Add a new launcher item (existing == null) or edit one.</summary>
public partial class EditItemDialog : Window
{
    private bool _labelEdited;
    private bool _settingLabel;

    public EditItemDialog(LauncherItem? existing, string groupName)
    {
        InitializeComponent();
        WindowCorners.Round(this);

        if (existing is null)
        {
            TitleBar.Text = string.IsNullOrEmpty(groupName) ? "Add item" : $"Add to “{groupName}”";
            UpdatePreview();
            return;
        }

        Title = "Edit item";
        TitleBar.Text = "Edit item";
        OkButton.Content = "Save";
        TargetBox.Text = existing.Target;
        ArgumentsBox.Text = existing.Arguments ?? "";
        AdminBox.IsChecked = existing.RunAsAdmin;
        SetLabel(existing.Label);
        _labelEdited = true; // keep the existing label even if the target changes
        UpdatePreview();
    }

    public string Target { get; private set; } = "";
    public string? Arguments { get; private set; }
    public string ItemLabel { get; private set; } = "";
    public bool RunAsAdmin { get; private set; }

    private void SetLabel(string text)
    {
        _settingLabel = true;
        LabelBox.Text = text;
        _settingLabel = false;
    }

    private static string CleanTarget(string text) => text.Trim().Trim('"');

    private void TargetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Suggest a label until the user types their own.
        if (!_labelEdited) SetLabel(LauncherItem.DefaultLabel(CleanTarget(TargetBox.Text)));
        UpdatePreview();
    }

    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingLabel) _labelEdited = LabelBox.Text.Length > 0;
        UpdatePreview();
    }

    /// <summary>Shows the tile as it will look: icon, label, what kind of target it is and whether it exists.</summary>
    private void UpdatePreview()
    {
        // Called from TextChanged while InitializeComponent is still wiring up controls.
        if (PreviewLabel is null || StatusChip is null) return;

        var target = CleanTarget(TargetBox.Text);
        if (target.Length == 0)
        {
            PreviewIcon.Source = null;
            PreviewLabel.Text = "New item";
            PreviewKind.Text = "Type or browse for a target";
            StatusChip.Visibility = Visibility.Collapsed;
            return;
        }

        var item = new LauncherItem(target, LabelBox.Text);
        PreviewIcon.Source = item.Icon;
        PreviewLabel.Text = string.IsNullOrWhiteSpace(LabelBox.Text) ? LauncherItem.DefaultLabel(target) : LabelBox.Text.Trim();

        var (kind, found) = Describe(item);
        PreviewKind.Text = kind;
        StatusChip.Visibility = Visibility.Visible;
        StatusChip.Background = (Brush)FindResource(found ? "SuccessSoftBrush" : "WarnSoftBrush");
        StatusText.Foreground = (Brush)FindResource(found ? "SuccessBrush" : "WarnBrush");
        StatusText.Text = found ? "Ready" : "Not found";
    }

    private static (string Kind, bool Found) Describe(LauncherItem item)
    {
        if (item.IsUrl) return ("Web link", true);
        if (!item.IsPath) return ("Command", true);
        if (Directory.Exists(item.Target)) return ("Folder · found on disk", true);
        if (!File.Exists(item.Target)) return ("Nothing at this path", false);

        var kind = Path.GetExtension(item.Target).ToLowerInvariant() switch
        {
            ".exe" => "Program",
            ".lnk" or ".url" => "Shortcut",
            ".bat" or ".cmd" or ".ps1" => "Script",
            _ => "File",
        };
        return ($"{kind} · found on disk", true);
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files|*.*",
            DereferenceLinks = false, // keep .lnk as the shortcut, not its target
        };
        if (dialog.ShowDialog(this) == true) TargetBox.Text = dialog.FileName;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog(this) == true) TargetBox.Text = dialog.FolderName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var target = CleanTarget(TargetBox.Text);
        if (target.Length == 0)
        {
            TargetBox.Focus();
            return;
        }

        Target = target;
        Arguments = string.IsNullOrWhiteSpace(ArgumentsBox.Text) ? null : ArgumentsBox.Text.Trim();
        ItemLabel = string.IsNullOrWhiteSpace(LabelBox.Text) ? LauncherItem.DefaultLabel(target) : LabelBox.Text.Trim();
        RunAsAdmin = AdminBox.IsChecked == true;
        DialogResult = true;
    }
}
