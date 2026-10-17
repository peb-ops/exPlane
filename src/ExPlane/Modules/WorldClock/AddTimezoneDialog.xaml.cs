using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExPlane.Services;

namespace ExPlane.Modules.WorldClock;

public partial class AddTimezoneDialog : Window
{
    private static readonly IReadOnlyList<TimeZoneInfo> AllZones = TimeZoneInfo.GetSystemTimeZones();

    public TimeZoneInfo? SelectedZone { get; private set; }
    public string ChosenLabel { get; private set; } = "";

    public AddTimezoneDialog()
    {
        InitializeComponent();
        WindowCorners.Round(this);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var terms = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ZoneList.ItemsSource = AllZones
            .Where(z => terms.All(t => z.DisplayName.Contains(t, StringComparison.OrdinalIgnoreCase)
                                    || z.Id.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (ZoneList.Items.Count > 0) ZoneList.SelectedIndex = 0;
    }

    /// <summary>"(UTC+09:00) Osaka, Sapporo, Tokyo" -> "Osaka, Sapporo, Tokyo".</summary>
    private static string DefaultLabel(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var close = name.IndexOf(") ", StringComparison.Ordinal);
        return close >= 0 ? name[(close + 2)..] : name;
    }

    private void Accept()
    {
        if (ZoneList.SelectedItem is not TimeZoneInfo zone) return;
        SelectedZone = zone;
        ChosenLabel = string.IsNullOrWhiteSpace(LabelBox.Text) ? DefaultLabel(zone) : LabelBox.Text.Trim();
        DialogResult = true;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Arrow keys move the list selection without leaving the search box.
        if (ZoneList.Items.Count == 0) return;
        if (e.Key == Key.Down) ZoneList.SelectedIndex = Math.Min(ZoneList.SelectedIndex + 1, ZoneList.Items.Count - 1);
        else if (e.Key == Key.Up) ZoneList.SelectedIndex = Math.Max(ZoneList.SelectedIndex - 1, 0);
        else return;
        ZoneList.ScrollIntoView(ZoneList.SelectedItem);
        e.Handled = true;
    }

    private void ZoneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoneList.SelectedItem is TimeZoneInfo zone) LabelBox.Text = DefaultLabel(zone);
    }

    private void ZoneList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void AddButton_Click(object sender, RoutedEventArgs e) => Accept();
}
