using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ExPlane.Modules.Launcher;

/// <summary>Add a new launcher item (existing == null) or edit one.</summary>
public partial class EditItemDialog : Window
{
    private bool _labelEdited;
    private bool _settingLabel;

    public EditItemDialog(LauncherItem? existing)
    {
        InitializeComponent();
        if (existing is null) return;

        Title = "Edit item";
        OkButton.Content = "Save";
        TargetBox.Text = existing.Target;
        ArgumentsBox.Text = existing.Arguments ?? "";
        SetLabel(existing.Label);
        _labelEdited = true; // keep the existing label even if the target changes
    }

    public string Target { get; private set; } = "";
    public string? Arguments { get; private set; }
    public string ItemLabel { get; private set; } = "";

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
    }

    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingLabel) _labelEdited = LabelBox.Text.Length > 0;
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
        DialogResult = true;
    }
}
