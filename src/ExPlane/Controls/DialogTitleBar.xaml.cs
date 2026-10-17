using System.Windows;
using System.Windows.Controls;

namespace ExPlane.Controls;

/// <summary>Title row for borderless dialogs (DialogWindowStyle): title text + close button; the row drags the window.</summary>
public partial class DialogTitleBar : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(DialogTitleBar), new PropertyMetadata(""));

    public DialogTitleBar() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
