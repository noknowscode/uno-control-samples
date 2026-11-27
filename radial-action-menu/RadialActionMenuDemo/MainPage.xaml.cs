using System.Collections.ObjectModel;
using RadialActionMenuDemo.Controls;
using Uno.Toolkit.UI;

namespace RadialActionMenuDemo;

public sealed partial class MainPage : Page
{
    private bool _picked;

    public MainPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);

        ContactList.ItemsSource = new[]
        {
            new Contact("AM", "Ada Moreau", "Site lead · Laval", IsSelected: true),
            new Contact("JP", "Jonas Petit", "Procurement"),
            new Contact("MT", "Mireille Tran", "Design review"),
            new Contact("SH", "Samir Haddad", "Field technician"),
            new Contact("LR", "Léa Roy", "Accounts"),
        };

        QuickActions.Items = RadialMenuIcons.ContactActions();
    }

    // App.AppRoot is null when a host (Studio, the Gallery) creates the page without App.OnLaunched.
    private FrameworkElement ThemeRoot => App.AppRoot ?? this;

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);

    private void OnMenuOpened(object? sender, EventArgs e) => _picked = false;

    private void OnItemSelected(object? sender, RadialMenuItemData item)
    {
        _picked = true;
        StatusText.Text = item.Label switch
        {
            "Contact" => "Opened the contact card for Ada Moreau",
            "Email" => "Writing an email to Ada Moreau",
            "Call" => "Calling Ada Moreau",
            "Note" => "Adding a note to Ada Moreau",
            _ => "Searching around Ada Moreau",
        };
    }

    private void OnMenuClosed(object? sender, EventArgs e)
    {
        if (!_picked) StatusText.Text = "Closed without an action";
        _picked = false;
    }
}

public sealed record Contact(string Initials, string Name, string Role, bool IsSelected = false)
{
    // The selected row: a light ink wash (the design fades it out to the right; a gradient bands on Skia).
    public Brush RowBackground => new SolidColorBrush(IsSelected
        ? Windows.UI.Color.FromArgb(0x0C, 0x25, 0x24, 0x22)
        : Microsoft.UI.Colors.Transparent);
}
