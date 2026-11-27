using HorizontalCalendar.Controls;
using HorizontalCalendar.Models;
using Uno.Toolkit.UI;

namespace HorizontalCalendar;

public sealed partial class MainPage : Page
{
    private readonly List<CalendarEvent> _events = SampleEvents.Create();

    public MainPage()
    {
        this.InitializeComponent();

        var monday = SampleEvents.WeekStart(DateTime.Today);
        Calendar.MinDate = monday.AddDays(-7);
        Calendar.MaxDate = monday.AddDays(13);
        Calendar.UnavailableDays = SampleEvents.UnavailableDays(DateTime.Today);
        Calendar.EventsProvider = (start, end) => _events.Where(e => e.StartTime.Date >= start && e.StartTime.Date <= end);
        Calendar.RefreshEvents();

        Loaded += (_, _) => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);
    }

    // The app's root in its own window; the page itself when a host (Studio, the Gallery) shows it.
    private FrameworkElement ThemeRoot => App.ThemeRoot ?? this;

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);

    private void OnAgendaStateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Calendar is not null) Calendar.AgendaState = (CalendarAgendaState)AgendaStatePicker.SelectedIndex;
    }

    private async void OnRetryRequested(object? sender, EventArgs e)
    {
        Calendar.AgendaState = CalendarAgendaState.Loading;
        await Task.Delay(1200);
        AgendaStatePicker.SelectedIndex = 0;
    }

    /// <summary>Adds a one-hour event at the first free hour of the selected day.</summary>
    private void OnAddEventRequested(object? sender, EventArgs e)
    {
        var day = Calendar.SelectedDate.Date;
        var start = Enumerable.Range(9, 9).Select(h => day.AddHours(h))
            .FirstOrDefault(t => !_events.Any(x => x.StartTime < t.AddHours(1) && x.EndTime > t), day.AddHours(18));
        _events.Add(new CalendarEvent { Title = "New event", Description = "Added from the demo", Location = "Office", StartTime = start, EndTime = start.AddHours(1) });
        Calendar.RefreshEvents();
    }

    private void OnEventOptionsRequested(object? sender, CalendarEventOptionsRequestedEventArgs e)
    {
        var delete = new MenuFlyoutItem { Text = "Delete" };
        delete.Click += (_, _) =>
        {
            _events.Remove(e.Event);
            Calendar.RefreshEvents();
        };
        var flyout = new MenuFlyout();
        flyout.Items.Add(delete);
        flyout.ShowAt(e.Target);
    }
}
