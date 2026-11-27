using Uno.Resizetizer;
using Uno.Toolkit.UI;

namespace AdaptiveInputDemo;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    /// <summary>
    /// The element the app's theme is applied to. Pages pass it to <see cref="SystemThemeHelper"/>, which works
    /// both in the app's own window and when Uno Platform Studio or the Gallery hosts the content.
    /// </summary>
    internal static FrameworkElement? ThemeRoot { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
#if DEBUG
        // Set APP_NO_HOTDESIGN=1 to run a Debug build without a DevServer (UseStudio keeps the window hidden then).
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            ThemeRoot = rootFrame;

            // The near-black Dark palette is the design's default; the page's switch changes it at runtime.
            SystemThemeHelper.SetRootTheme(rootFrame, darkMode: true);
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.SetWindowIcon();
        MainWindow.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }
}
