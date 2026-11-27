using Uno.Toolkit.UI;

namespace ReticleLab.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) =>
        {
            ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);
            ViewModel.RunAgain();
        };
        Unloaded += (_, _) => ViewModel.Stop();
    }

    public MainViewModel ViewModel { get; } = new();

    // App.AppRoot is null when a host (Studio, the Gallery) creates the page without App.OnLaunched.
    private FrameworkElement ThemeRoot => App.AppRoot ?? this;

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);
}
