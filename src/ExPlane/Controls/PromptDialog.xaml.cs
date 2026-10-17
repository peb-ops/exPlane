using System.Windows;
using ExPlane.Services;

namespace ExPlane.Controls;

/// <summary>Single-line text prompt (e.g. rename) in the plane's dialog style.</summary>
public partial class PromptDialog : Window
{
    private PromptDialog()
    {
        InitializeComponent();
        WindowCorners.Round(this);
    }

    /// <returns>The trimmed text, or null if cancelled.</returns>
    public static string? Ask(Window? owner, string title, string prompt, string initial)
    {
        var dialog = new PromptDialog { Owner = owner, Title = title };
        dialog.TitleBar.Text = title;
        dialog.PromptLabel.Content = prompt;
        dialog.InputBox.Text = initial;
        dialog.InputBox.SelectAll();
        return dialog.ShowDialog() == true ? dialog.InputBox.Text.Trim() : null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            InputBox.Focus();
            return;
        }
        DialogResult = true;
    }
}
