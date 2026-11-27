using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using PrecisionDial.Controls;
using Uno.Toolkit.UI;

namespace PrecisionDial.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);

        // The menu dial's items: Phosphor Icons glyphs from the bundled font, drawn by the dial's MenuIconRenderer.
        Dial.MenuItems = new List<DialMenuItem>
        {
            new() { Label = "MUSIC", Icon = "", Tag = "music" },       // ph-music-note
            new() { Label = "RADIO", Icon = "", Tag = "radio" },       // ph-radio
            new() { Label = "PODCAST", Icon = "", Tag = "podcast" },   // ph-microphone
            new() { Label = "VIDEO", Icon = "", Tag = "video" },       // ph-video-camera
            new() { Label = "SETTINGS", Icon = "", Tag = "settings" }, // ph-gear
        };
    }

    // The app's root when the app runs in its own window; the page itself when a host (Studio, the Gallery)
    // shows it without running App.OnLaunched.
    private FrameworkElement ThemeRoot => App.ThemeRoot ?? this;

    // xaml-lint: allow codebehind - the theme switch has no model; SystemThemeHelper is the Toolkit's API for it
    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);

    // WORKAROUND(TabBar selection indicator is a tab stop): Toolkit 11.0.1's TabBarSelectionIndicatorPresenter takes
    // keyboard focus with no focus visual or name, so Tab stopped on nothing between MENU and the dial. An implicit
    // style in TabBar.Resources does not reach it. Remove when the Toolkit sets IsTabStop="False" on it.
    // xaml-lint: allow codebehind - see WORKAROUND
    private void OnModeSwitchLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var presenter in Descendants<TabBarSelectionIndicatorPresenter>(ModeSwitch))
        {
            presenter.IsTabStop = false;
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    // x:Bind functions: the segmented switch picks the mode; the labels follow it.
    public DialMode ModeFor(int index) => index == 1 ? DialMode.Menu : DialMode.Value;

    public string LabelFor(DialMode mode) => mode == DialMode.Menu ? "MODE" : "VOLUME";

    public string HintFor(DialMode mode) => mode == DialMode.Menu
        ? "DRAG OR ← → TO CHOOSE · ENTER TO CONFIRM"
        : "DRAG OR ↑ ↓ · SHIFT FOR 0.1";

    public string ReadoutFor(DialMode mode, double value, DialMenuItem? item) => mode == DialMode.Menu
        ? item?.Label ?? string.Empty
        : value.ToString("0.#", CultureInfo.InvariantCulture);
}
