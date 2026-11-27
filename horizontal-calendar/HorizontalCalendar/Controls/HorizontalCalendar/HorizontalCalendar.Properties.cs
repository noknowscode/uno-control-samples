using HorizontalCalendar.Models;

namespace HorizontalCalendar.Controls;

public partial class HorizontalCalendar
{
    public static DependencyProperty SelectedDateProperty { get; } = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime), typeof(HorizontalCalendar),
        new PropertyMetadata(DateTime.Today, (d, e) => ((HorizontalCalendar)d).OnSelectedDateChanged((DateTime)e.OldValue, (DateTime)e.NewValue)));

    /// <summary>The selected day (two-way bindable). Unavailable days and days outside MinDate..MaxDate are refused.</summary>
    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public static DependencyProperty TodayProperty { get; } = DependencyProperty.Register(
        nameof(Today), typeof(DateTime), typeof(HorizontalCalendar),
        new PropertyMetadata(DateTime.Today, (d, _) => ((HorizontalCalendar)d).RebuildDays()));

    /// <summary>The day drawn with the today ring; days before it are past. Defaults to the current date.</summary>
    public DateTime Today
    {
        get => (DateTime)GetValue(TodayProperty);
        set => SetValue(TodayProperty, value);
    }

    public static DependencyProperty MinDateProperty { get; } = DependencyProperty.Register(
        nameof(MinDate), typeof(DateTime?), typeof(HorizontalCalendar),
        new PropertyMetadata(null, (d, _) => ((HorizontalCalendar)d).UpdateHeader()));

    /// <summary>First selectable day; paging stops at its week.</summary>
    public DateTime? MinDate
    {
        get => (DateTime?)GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    public static DependencyProperty MaxDateProperty { get; } = DependencyProperty.Register(
        nameof(MaxDate), typeof(DateTime?), typeof(HorizontalCalendar),
        new PropertyMetadata(null, (d, _) => ((HorizontalCalendar)d).UpdateHeader()));

    /// <summary>Last selectable day; paging stops at its week.</summary>
    public DateTime? MaxDate
    {
        get => (DateTime?)GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    public static DependencyProperty UnavailableDaysProperty { get; } = DependencyProperty.Register(
        nameof(UnavailableDays), typeof(IReadOnlyDictionary<DateTime, CalendarUnavailableDay>), typeof(HorizontalCalendar),
        new PropertyMetadata(null, (d, _) => ((HorizontalCalendar)d).RebuildDays()));

    /// <summary>Days that can't be booked: hatched, focusable, not selectable, with a label and a reason.</summary>
    public IReadOnlyDictionary<DateTime, CalendarUnavailableDay>? UnavailableDays
    {
        get => (IReadOnlyDictionary<DateTime, CalendarUnavailableDay>?)GetValue(UnavailableDaysProperty);
        set => SetValue(UnavailableDaysProperty, value);
    }

    public static DependencyProperty AgendaStateProperty { get; } = DependencyProperty.Register(
        nameof(AgendaState), typeof(CalendarAgendaState), typeof(HorizontalCalendar),
        new PropertyMetadata(CalendarAgendaState.Ready, (d, _) => ((HorizontalCalendar)d).OnAgendaStateChanged()));

    /// <summary>Ready, Loading (skeleton rows, ticks hidden, dates usable) or Error (only the agenda is replaced).</summary>
    public CalendarAgendaState AgendaState
    {
        get => (CalendarAgendaState)GetValue(AgendaStateProperty);
        set => SetValue(AgendaStateProperty, value);
    }

    public static DependencyProperty ErrorTitleProperty { get; } = DependencyProperty.Register(
        nameof(ErrorTitle), typeof(string), typeof(HorizontalCalendar), new PropertyMetadata("Events didn’t load"));

    public string ErrorTitle
    {
        get => (string)GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    public static DependencyProperty ErrorMessageProperty { get; } = DependencyProperty.Register(
        nameof(ErrorMessage), typeof(string), typeof(HorizontalCalendar), new PropertyMetadata("Your selected day is kept."));

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public static DependencyProperty DisplayModeProperty { get; } = DependencyProperty.Register(
        nameof(DisplayMode), typeof(CalendarDisplayMode), typeof(HorizontalCalendar),
        new PropertyMetadata(CalendarDisplayMode.Auto, (d, _) => ((HorizontalCalendar)d).UpdateDisplayMode()));

    /// <summary>Regular, Compact (the phone layout), or Auto: Compact under <see cref="CompactMaxWidth"/>.</summary>
    public CalendarDisplayMode DisplayMode
    {
        get => (CalendarDisplayMode)GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    public static DependencyProperty CompactMaxWidthProperty { get; } = DependencyProperty.Register(
        nameof(CompactMaxWidth), typeof(double), typeof(HorizontalCalendar),
        new PropertyMetadata(600d, (d, _) => ((HorizontalCalendar)d).UpdateDisplayMode()));

    public double CompactMaxWidth
    {
        get => (double)GetValue(CompactMaxWidthProperty);
        set => SetValue(CompactMaxWidthProperty, value);
    }

    public static DependencyProperty LegendVisibilityProperty { get; } = DependencyProperty.Register(
        nameof(LegendVisibility), typeof(Visibility), typeof(HorizontalCalendar), new PropertyMetadata(Visibility.Visible));

    /// <summary>The Selected / Today / ticks / Unavailable legend (never shown in the compact layout).</summary>
    public Visibility LegendVisibility
    {
        get => (Visibility)GetValue(LegendVisibilityProperty);
        set => SetValue(LegendVisibilityProperty, value);
    }

    /// <summary>
    /// Returns the events between two days (inclusive). Called for the shown week and its neighbours; call
    /// <see cref="RefreshEvents"/> after the data changes.
    /// </summary>
    public Func<DateTime, DateTime, IEnumerable<CalendarEvent>>? EventsProvider { get; set; }

    /// <summary>Raised when the user (or code) selects a day.</summary>
    public event EventHandler<DateTime>? SelectedDateChanged;

    /// <summary>Raised by the "Add event" buttons.</summary>
    public event EventHandler? AddEventRequested;

    /// <summary>Raised by Retry in the error state.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Raised by an agenda row's "More options" button.</summary>
    public event EventHandler<CalendarEventOptionsRequestedEventArgs>? EventOptionsRequested;
}
