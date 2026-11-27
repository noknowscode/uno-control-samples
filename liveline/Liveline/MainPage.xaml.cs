using System.Collections.ObjectModel;
using Liveline.Models;
using Uno.Toolkit.UI;
using Windows.UI.ViewManagement;

namespace Liveline.Demo;

/// <summary>
/// Hosts one card on a simulated feed: Connecting for two seconds, then one sample a second from the prototype's
/// seeded random walk (15 minutes of history, so every window has data). "Drop connection" moves the card to
/// Disconnected; its Reconnect button brings the feed back.
/// </summary>
public sealed partial class MainPage : Page
{
    private static readonly TimeSpan History = TimeSpan.FromMinutes(15);

    private readonly ObservableCollection<LivelinePoint> _data = [];
    private readonly DispatcherTimer _sampleTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _connectTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private long _seed = 7;
    private double _value = 54;

    public MainPage()
    {
        this.InitializeComponent();

        // Uno reports AnimationsEnabled on Android only; elsewhere this keeps the glide on.
        Chart.IsMotionReduced = !new UISettings().AnimationsEnabled;
        Chart.Data = _data;
        Chart.State = LivelineState.Connecting;
        Chart.ReconnectRequested += (_, _) => Connect();

        _sampleTimer.Tick += (_, _) => AddSample(DateTimeOffset.Now);
        _connectTimer.Tick += (_, _) =>
        {
            _connectTimer.Stop();
            if (_data.Count == 0)
            {
                var now = DateTimeOffset.Now;
                for (var t = History.TotalSeconds; t > 0; t--)
                {
                    _data.Add(new LivelinePoint(now.AddSeconds(-t), Next()));
                }
            }

            Chart.State = LivelineState.Live;
            ConnectionToggle.IsChecked = false;
            _sampleTimer.Start();
        };

        Loaded += (_, _) => _connectTimer.Start();
        Unloaded += (_, _) =>
        {
            _connectTimer.Stop();
            _sampleTimer.Stop();
        };
    }

    // The prototype's walk: a Park–Miller generator from seed 7, pulled toward 55 and kept inside 41–69.
    private double Next()
    {
        _seed = _seed * 16807 % 2147483647;
        var r = _seed / 2147483647.0 - 0.5;
        _value = Math.Clamp(_value + r * 3.8 + (55 - _value) * 0.06, 41, 69);
        return _value;
    }

    private void AddSample(DateTimeOffset time)
    {
        _data.Add(new LivelinePoint(time, Next()));
        while (_data.Count > 0 && _data[0].Time < time - History)
        {
            _data.RemoveAt(0);
        }
    }

    private void Connect()
    {
        Chart.State = LivelineState.Connecting;
        _connectTimer.Start();
    }

    private void OnConnectionToggleClick(object sender, RoutedEventArgs e)
    {
        if (ConnectionToggle.IsChecked == true)
        {
            _sampleTimer.Stop();
            _connectTimer.Stop();
            Chart.State = LivelineState.Disconnected;
        }
        else
        {
            Connect();
        }
    }

    private void OnThemeToggleLoaded(object sender, RoutedEventArgs e)
        => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(App.AppRoot ?? this);

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(App.AppRoot ?? this, ThemeToggle.IsChecked == true);
}
