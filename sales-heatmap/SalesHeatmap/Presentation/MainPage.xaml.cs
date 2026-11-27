using Uno.Toolkit.UI;

namespace SalesHeatmap.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private void OnThemeToggleLoaded(object sender, RoutedEventArgs e)
        => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(App.AppRoot ?? this);

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(App.AppRoot ?? this, ThemeToggle.IsChecked == true);
}
