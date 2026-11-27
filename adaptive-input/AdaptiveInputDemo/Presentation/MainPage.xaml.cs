using Microsoft.UI.Xaml.Media;
using Uno.Toolkit.UI;

namespace AdaptiveInputDemo.Presentation;

public sealed partial class MainPage : Page
{
    private const string Calendar = "M4 7a2 2 0 0 1 2 -2h12a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-12z M16 3v4 M8 3v4 M4 11h16";
    private const string Palette = "M12 21a9 9 0 0 1 0 -18c4.97 0 9 3.582 9 8c0 1.06 -.474 2.078 -1.318 2.828c-.844 .75 -1.989 1.172 -3.182 1.172h-2.5a2 2 0 0 0 -1 3.75a1.3 1.3 0 0 1 -1 2.25 M7.5 10.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 M11.5 7.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 M15.5 10.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0";
    private const string At = "M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0 M16 12v1.5a2.5 2.5 0 0 0 5 0v-1.5a9 9 0 1 0 -5.5 8.28";
    private const string Hash = "M5 9l14 0 M5 15l14 0 M11 4l-4 16 M17 4l-4 16";
    private const string Note = "M13 20l7 -7 M13 20v-6a1 1 0 0 1 1 -1h6v-7a2 2 0 0 0 -2 -2h-12a2 2 0 0 0 -2 2v12a2 2 0 0 0 2 2h7";

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);
        ExampleList.ItemsSource = new[]
        {
            new Example("tomorrow", "Date", Calendar),
            new Example("fri 3pm", "Date and time", Calendar),
            new Example("#FF5500", "Colour", Palette),
            new Example("@ad", "Mention", At),
            new Example("#urgent", "Tag", Hash),
            new Example("lunch with Léa", "Stays text", Note),
        };
    }

    // The app's root when the app runs in its own window; the page itself when a host (Studio, the Gallery)
    // shows it without running App.OnLaunched.
    private FrameworkElement ThemeRoot => App.ThemeRoot ?? this;

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);

    private void OnExampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string example })
        {
            MainInput.Clear();
            MainInput.Value = example;
            MainInput.FocusField();
        }
    }
}

public sealed record Example(string Text, string ReadsAs, string IconData)
{
    public Geometry Icon => (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), IconData);
    public string AutomationName => $"{Text}, reads as {ReadsAs}";
}
