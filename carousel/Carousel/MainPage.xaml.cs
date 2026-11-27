using Uno.Toolkit.UI;

namespace CarouselDemo;

public sealed partial class MainPage : Page
{
    private readonly IReadOnlyList<Plate> _plates = Plate.LowTideSeries();

    public MainPage()
    {
        this.InitializeComponent();
        Plates.ItemsSource = _plates;
        Plates.SelectedIndex = 1;
    }

    private void OnFailImageClick(object sender, RoutedEventArgs e)
    {
        if (Plates.SelectedIndex >= 0) _plates[Plates.SelectedIndex].Break();
    }

    private void OnPlateRetryRequested(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Plate plate }) plate.Restore();
    }

    private void OnThemeToggleLoaded(object sender, RoutedEventArgs e)
        => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(App.AppRoot ?? this);

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(App.AppRoot ?? this, ThemeToggle.IsChecked == true);
}
