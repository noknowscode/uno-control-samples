using HorizontalCalendar.Models;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace HorizontalCalendar.Controls;

/// <summary>
/// A week strip that shows how full each day is (one tick per event, up to four), with the selected day's agenda
/// below. A cobalt pill marks the selected day and slides between days; the strip slides between weeks. Unavailable
/// days are hatched and can't be selected. The agenda has Ready, empty-day, Loading and Error states.
/// Keys on the strip: ← → day, PgUp / PgDn week, T today.
/// </summary>
[TemplatePart(Name = PART_WeekPanel, Type = typeof(CalendarWeekPanel))]
[TemplatePart(Name = PART_Pill, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PART_Viewport, Type = typeof(FrameworkElement))]
[TemplateVisualState(GroupName = "DisplayStates", Name = "Regular")]
[TemplateVisualState(GroupName = "DisplayStates", Name = "Compact")]
[TemplateVisualState(GroupName = "AgendaStates", Name = "Events")]
[TemplateVisualState(GroupName = "AgendaStates", Name = "Empty")]
[TemplateVisualState(GroupName = "AgendaStates", Name = "Loading")]
[TemplateVisualState(GroupName = "AgendaStates", Name = "Error")]
public partial class HorizontalCalendar : Control
{
    private const string PART_WeekPanel = "PART_WeekPanel";
    private const string PART_Pill = "PART_Pill";
    private const string PART_Viewport = "PART_Viewport";

    // Pill 380 ms, strip 460 ms, agenda 220 / 320 ms after 80 ms: cubic-bezier(.3, .7, .2, 1).
    private static readonly TimeSpan PillDuration = TimeSpan.FromMilliseconds(380);
    private static readonly TimeSpan StripDuration = TimeSpan.FromMilliseconds(460);
    private static readonly TimeSpan AgendaDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan AgendaFade = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan AgendaRise = TimeSpan.FromMilliseconds(320);

    private static KeySpline Curve() => new() { ControlPoint1 = new(0.3, 0.7), ControlPoint2 = new(0.2, 1) };
    private static KeySpline Ease() => new() { ControlPoint1 = new(0.25, 0.1), ControlPoint2 = new(0.25, 1) };

    private CalendarWeekPanel? _weekPanel;
    private FrameworkElement? _pill;
    private FrameworkElement? _viewport;
    private FrameworkElement? _agendaHost;
    private TextBlock? _monthText;
    private TextBlock? _rangeText;
    private TextBlock? _agendaTitle;
    private TextBlock? _agendaSummary;
    private TextBlock? _emptyTitle;
    private ItemsControl? _rows;
    private ItemsControl? _compactRows;
    private ButtonBase? _previousWeekButton;
    private ButtonBase? _nextWeekButton;
    private readonly List<ButtonBase> _wiredButtons = [];

    private readonly TranslateTransform _stripTransform = new();
    private readonly TranslateTransform _pillTransform = new();
    private readonly TranslateTransform _agendaTransform = new();
    private Storyboard? _stripStoryboard;
    private Storyboard? _pillStoryboard;
    private Storyboard? _agendaStoryboard;

    private readonly List<CalendarDayButton> _days = [];
    private readonly Dictionary<DateTime, List<CalendarEvent>> _events = [];
    private DateTime _weekStart;
    private bool _isCompact;
    private bool _suppressSelectionCallback;

    public HorizontalCalendar()
    {
        DefaultStyleKey = typeof(HorizontalCalendar);
        _weekStart = WeekOf(DateTime.Today);
        SizeChanged += (_, _) => UpdateDisplayMode();
    }

    /// <summary>The first day of the week the strip shows.</summary>
    public DateTime WeekStart => _weekStart;

    /// <summary>The events of the selected day, in start order.</summary>
    public IReadOnlyList<CalendarEvent> SelectedDayEvents => EventsOn(SelectedDate);

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        foreach (var button in _wiredButtons) button.Click -= OnTemplateButtonClick;
        _wiredButtons.Clear();

        _weekPanel = GetTemplateChild(PART_WeekPanel) as CalendarWeekPanel;
        _pill = GetTemplateChild(PART_Pill) as FrameworkElement;
        _viewport = GetTemplateChild(PART_Viewport) as FrameworkElement;
        _agendaHost = GetTemplateChild("AgendaHost") as FrameworkElement;
        _monthText = GetTemplateChild("MonthText") as TextBlock;
        _rangeText = GetTemplateChild("RangeText") as TextBlock;
        _agendaTitle = GetTemplateChild("AgendaTitle") as TextBlock;
        _agendaSummary = GetTemplateChild("AgendaSummary") as TextBlock;
        _emptyTitle = GetTemplateChild("EmptyTitle") as TextBlock;
        _rows = GetTemplateChild("Rows") as ItemsControl;
        _compactRows = GetTemplateChild("CompactRows") as ItemsControl;
        _previousWeekButton = GetTemplateChild("PreviousWeekButton") as ButtonBase;
        _nextWeekButton = GetTemplateChild("NextWeekButton") as ButtonBase;

        foreach (var name in new[] { "TodayButton", "PreviousWeekButton", "NextWeekButton", "AddEventButton", "CompactAddEventButton", "EmptyAddEventButton", "RetryButton" })
        {
            if (GetTemplateChild(name) is ButtonBase button)
            {
                button.Click += OnTemplateButtonClick;
                _wiredButtons.Add(button);
            }
        }

        if (_weekPanel != null) _weekPanel.RenderTransform = _stripTransform;
        if (_pill != null) _pill.RenderTransform = _pillTransform;
        if (_agendaHost != null) _agendaHost.RenderTransform = _agendaTransform;
        if (_viewport != null)
        {
            _viewport.SizeChanged += (_, e) => _viewport.Clip = new RectangleGeometry { Rect = new(0, 0, e.NewSize.Width, e.NewSize.Height) };
        }

        _weekStart = WeekOf(SelectedDate);
        UpdateDisplayMode();
        RefreshEvents();
    }

    #region Public API

    /// <summary>Reloads the events of the shown week and its neighbours from <see cref="EventsProvider"/>.</summary>
    public void RefreshEvents()
    {
        _events.Clear();
        if (EventsProvider is { } provider)
        {
            var from = _weekStart.AddDays(-7);
            var to = _weekStart.AddDays(13);
            foreach (var calendarEvent in provider(from, to))
            {
                var day = calendarEvent.StartTime.Date;
                if (!_events.TryGetValue(day, out var list)) _events[day] = list = [];
                list.Add(calendarEvent);
            }
            foreach (var list in _events.Values) list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }
        RebuildDays();
        UpdateAgenda(false);
    }

    /// <summary>Selects today and shows its week.</summary>
    public void GoToToday() => SelectDate(Today.Date);

    /// <summary>Selects a day (if it can be selected) and shows its week. Returns false when it can't be selected.</summary>
    public bool SelectDate(DateTime date)
    {
        date = date.Date;
        if (!IsSelectable(date)) return false;
        SelectedDate = date;
        return true;
    }

    /// <summary>Shows the previous week (the selection stays where it is).</summary>
    public void PreviousWeek() => PageTo(_weekStart.AddDays(-7));

    /// <summary>Shows the next week (the selection stays where it is).</summary>
    public void NextWeek() => PageTo(_weekStart.AddDays(7));

    #endregion

    #region Selection and paging

    private static DateTime WeekOf(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday first
        return date.Date.AddDays(-offset);
    }

    private CalendarUnavailableDay? UnavailableOn(DateTime date)
        => UnavailableDays != null && UnavailableDays.TryGetValue(date.Date, out var day) ? day : null;

    private bool InRange(DateTime date)
        => (MinDate is not DateTime min || date >= min.Date) && (MaxDate is not DateTime max || date <= max.Date);

    private bool IsSelectable(DateTime date) => InRange(date) && UnavailableOn(date) == null;

    private bool CanShowWeek(DateTime weekStart)
        => (MinDate is not DateTime min || weekStart.AddDays(6) >= min.Date) && (MaxDate is not DateTime max || weekStart <= max.Date);

    private IReadOnlyList<CalendarEvent> EventsOn(DateTime date)
        => _events.TryGetValue(date.Date, out var list) ? list : [];

    private void OnSelectedDateChanged(DateTime oldValue, DateTime newValue)
    {
        if (_suppressSelectionCallback) return;
        var date = newValue.Date;
        if (date != newValue || !IsSelectable(date))
        {
            _suppressSelectionCallback = true;
            SelectedDate = IsSelectable(date) ? date : oldValue;
            _suppressSelectionCallback = false;
            if (!IsSelectable(date)) return;
        }

        var targetWeek = WeekOf(date);
        if (_weekPanel == null)
        {
            _weekStart = targetWeek;
        }
        else if (targetWeek != _weekStart)
        {
            MovePill(oldValue.Date, date);
            PageTo(targetWeek);
        }
        else
        {
            MovePill(oldValue.Date, date);
            RefreshDayStates(true);
        }

        UpdateAgenda(true);
        SelectedDateChanged?.Invoke(this, date);
    }

    /// <summary>Moves the pill to the new day's column and slides it from where it was (380 ms).</summary>
    private void MovePill(DateTime from, DateTime to)
    {
        if (_weekPanel == null || _pill == null) return;
        var origin = _weekStart.AddDays(-7);
        var fromColumn = (int)(from - origin).TotalDays;
        var toColumn = (int)(to - origin).TotalDays;
        _pillStoryboard?.Stop();
        _pillStoryboard = null;
        _pillTransform.X = 0;

        if (toColumn < 0 || toColumn > 20)
        {
            _weekPanel.PillColumn = -1;
            return;
        }
        _weekPanel.PillColumn = toColumn;
        if (fromColumn < 0 || fromColumn > 20 || fromColumn == toColumn || _weekPanel.ColumnWidth <= 0) return;

        _pillTransform.X = (fromColumn - toColumn) * _weekPanel.ColumnWidth;
        _pillStoryboard = Animate(_pillTransform, "X", 0, PillDuration, Curve(), TimeSpan.Zero, () => _pillTransform.X = 0);
    }

    private void PageTo(DateTime weekStart)
    {
        if (weekStart == _weekStart || !CanShowWeek(weekStart)) return;
        var direction = Math.Sign((weekStart - _weekStart).TotalDays);
        var adjacent = Math.Abs((weekStart - _weekStart).TotalDays) == 7;

        _stripStoryboard?.Stop();
        _stripStoryboard = null;

        if (!adjacent || _weekPanel == null || _weekPanel.ActualWidth <= 0)
        {
            _weekStart = weekStart;
            _stripTransform.X = 0;
            RefreshEvents();
            return;
        }

        var target = -direction * _weekPanel.ActualWidth;
        _stripStoryboard = Animate(_stripTransform, "X", target, StripDuration, Curve(), TimeSpan.Zero, () =>
        {
            _stripStoryboard = null;
            _weekStart = weekStart;
            _stripTransform.X = 0;
            RefreshEvents();
        });
        UpdateHeader(weekStart);
    }

    private void MoveSelection(int direction)
    {
        var date = SelectedDate.AddDays(direction);
        for (int guard = 0; guard < 366 && InRange(date); guard++, date = date.AddDays(direction))
        {
            if (IsSelectable(date))
            {
                SelectedDate = date;
                return;
            }
        }
    }

    private void MoveWeek(int direction)
    {
        // PgDn: the first selectable day from a week later onwards; PgUp: the first one from a week earlier backwards.
        var date = SelectedDate.AddDays(7 * direction);
        for (int guard = 0; guard < 7 && InRange(date); guard++, date = date.AddDays(-direction))
        {
            if (IsSelectable(date))
            {
                SelectedDate = date;
                return;
            }
        }
        for (date = SelectedDate.AddDays(7 * direction); InRange(date) && Math.Abs((date - SelectedDate).TotalDays) < 21; date = date.AddDays(direction))
        {
            if (IsSelectable(date))
            {
                SelectedDate = date;
                return;
            }
        }
    }

    #endregion

    #region Building the strip

    private void RebuildDays()
    {
        if (_weekPanel == null) return;

        foreach (var day in _days)
        {
            day.Click -= OnDayClick;
            _weekPanel.Children.Remove(day);
        }
        _days.Clear();

        var origin = _weekStart.AddDays(-7);
        for (int i = 0; i < 21; i++)
        {
            var day = new CalendarDayButton { Date = origin.AddDays(i) };
            day.Click += OnDayClick;
            _days.Add(day);
            _weekPanel.Children.Add(day);
        }

        var column = (int)(SelectedDate.Date - origin).TotalDays;
        _weekPanel.PillColumn = column is >= 0 and <= 20 ? column : -1;
        RefreshDayStates(false);
        UpdateHeader();
    }

    private void RefreshDayStates(bool useTransitions)
    {
        var today = Today.Date;
        var showTicks = AgendaState != CalendarAgendaState.Loading;
        foreach (var day in _days)
        {
            var date = day.Date;
            var unavailable = UnavailableOn(date);
            var count = EventsOn(date).Count;
            day.Weekday = date.ToString("ddd", CalendarText.Culture).ToUpperInvariant();
            day.DayNumber = date.Day.ToString(CalendarText.Culture);
            day.TickCount = count;
            day.ShowTicks = showTicks;
            day.IsSelectedDay = date == SelectedDate.Date;
            day.IsToday = date == today;
            day.IsPast = date < today;
            day.UnavailableLabel = unavailable?.Label;
            day.IsCompact = _isCompact;
            day.IsTabStop = date >= _weekStart && date < _weekStart.AddDays(7);
            day.Height = _isCompact ? 66 : 84;

            var name = $"{date.ToString("dddd, MMMM d", CalendarText.Culture)}{(date == today ? ", today" : string.Empty)}, "
                + (unavailable != null ? $"{unavailable.Reason}, not bookable" : count == 0 ? "no events" : CalendarText.Events(count));
            AutomationProperties.SetName(day, name);
            ToolTipService.SetToolTip(day, unavailable?.Reason);
            day.Refresh(useTransitions);
        }
        if (_weekPanel != null) _weekPanel.CellInset = _isCompact ? 2 : 4;
    }

    private void OnDayClick(object sender, RoutedEventArgs e)
    {
        if (sender is CalendarDayButton day && !day.IsUnavailable) SelectDate(day.Date);
    }

    private static string Format(DateTime date, string format) => date.ToString(format, CalendarText.Culture);

    private void UpdateHeader() => UpdateHeader(_weekStart);

    private void UpdateHeader(DateTime weekStart)
    {
        var end = weekStart.AddDays(6);
        var sameMonth = weekStart.Month == end.Month;
        if (_monthText != null)
        {
            _monthText.Text = _isCompact
                ? (sameMonth ? weekStart.ToString("MMMM", CalendarText.Culture) : $"{Format(weekStart, "MMM")} – {Format(end, "MMM")}")
                : (sameMonth ? weekStart.ToString("MMMM yyyy", CalendarText.Culture) : $"{Format(weekStart, "MMM")} – {Format(end, "MMM yyyy")}");
        }
        if (_rangeText != null)
        {
            var range = sameMonth ? $"{Format(weekStart, "MMM d")} – {end.Day}" : $"{Format(weekStart, "MMM d")} – {Format(end, "MMM d")}";
            if (_rangeText.Text != range)
            {
                _rangeText.Text = range;
                AutomationProperties.SetName(_rangeText, $"Week of {weekStart.ToString("MMMM d", CalendarText.Culture)} to {end.ToString("MMMM d", CalendarText.Culture)}");
                if (IsLoaded) FrameworkElementAutomationPeer.FromElement(_rangeText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }
        if (_previousWeekButton != null) _previousWeekButton.IsEnabled = CanShowWeek(weekStart.AddDays(-7));
        if (_nextWeekButton != null) _nextWeekButton.IsEnabled = CanShowWeek(weekStart.AddDays(7));
    }

    #endregion

    #region Agenda

    private void OnAgendaStateChanged()
    {
        RefreshDayStates(true);
        UpdateAgenda(false);
        if (AgendaState == CalendarAgendaState.Error && GetTemplateChild("ErrorTitleText") is UIElement error)
            FrameworkElementAutomationPeer.FromElement(error)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void UpdateAgenda(bool animate)
    {
        var date = SelectedDate.Date;
        var events = EventsOn(date);

        if (_agendaTitle != null)
        {
            var format = _isCompact ? "dddd, MMM d" : "dddd, MMMM d";
            _agendaTitle.Text = date.ToString(format, CalendarText.Culture) + (date == Today.Date && !_isCompact ? " · Today" : string.Empty);
        }
        if (_agendaSummary != null)
        {
            var total = events.Aggregate(TimeSpan.Zero, (sum, e) => sum + (e.EndTime - e.StartTime));
            _agendaSummary.Text = events.Count == 0 ? "No events" : $"{CalendarText.Events(events.Count)} · {CalendarText.Duration(total)}";
        }
        if (_emptyTitle != null) _emptyTitle.Text = $"Nothing on {date.ToString("dddd", CalendarText.Culture)}";

        var options = new CalendarCommand(OnRowOptions);
        var items = events.Select(e => new CalendarAgendaItem(e, options)).ToList();
        if (_rows != null) _rows.ItemsSource = items;
        if (_compactRows != null) _compactRows.ItemsSource = items;

        var state = AgendaState switch
        {
            CalendarAgendaState.Loading => "Loading",
            CalendarAgendaState.Error => "Error",
            _ => events.Count == 0 ? "Empty" : "Events",
        };
        VisualStateManager.GoToState(this, state, false);

        if (animate && _agendaHost != null)
        {
            _agendaStoryboard?.Stop();
            _agendaHost.Opacity = 0;
            _agendaTransform.Y = 6;
            var storyboard = new Storyboard();
            storyboard.Children.Add(Keyframe(_agendaHost, nameof(UIElement.Opacity), 1, AgendaFade, Ease(), AgendaDelay));
            storyboard.Children.Add(Keyframe(_agendaTransform, "Y", 0, AgendaRise, Curve(), AgendaDelay));
            storyboard.Completed += (_, _) =>
            {
                if (_agendaStoryboard != storyboard) return;
                storyboard.Stop();
                _agendaHost.Opacity = 1;
                _agendaTransform.Y = 0;
            };
            _agendaStoryboard = storyboard;
            storyboard.Begin();
        }
    }

    private void OnRowOptions(object? parameter)
    {
        if (parameter is not FrameworkElement { DataContext: CalendarAgendaItem item } target) return;
        EventOptionsRequested?.Invoke(this, new CalendarEventOptionsRequestedEventArgs(item.Event, target));
    }

    #endregion

    #region Buttons, keys, display mode

    private void OnTemplateButtonClick(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.Name)
        {
            case "TodayButton": GoToToday(); break;
            case "PreviousWeekButton": PreviousWeek(); break;
            case "NextWeekButton": NextWeek(); break;
            case "RetryButton": RetryRequested?.Invoke(this, EventArgs.Empty); break;
            default: AddEventRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.OriginalSource is not CalendarDayButton) return;

        switch (e.Key)
        {
            case VirtualKey.Left: MoveSelection(-1); break;
            case VirtualKey.Right: MoveSelection(1); break;
            case VirtualKey.PageUp: MoveWeek(-1); break;
            case VirtualKey.PageDown: MoveWeek(1); break;
            case VirtualKey.T: GoToToday(); break;
            default: return;
        }
        e.Handled = true;
        FocusSelectedDay();
    }

    private void FocusSelectedDay()
    {
        // The strip may be sliding to another week; focus the day once it has been rebuilt.
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (_stripStoryboard != null)
            {
                _stripStoryboard.Completed += (_, _) => DispatcherQueue?.TryEnqueue(FocusSelectedDayNow);
                return;
            }
            FocusSelectedDayNow();
        });
    }

    private void FocusSelectedDayNow()
        => _days.FirstOrDefault(d => d.Date == SelectedDate.Date)?.Focus(FocusState.Keyboard);

    private void UpdateDisplayMode()
    {
        var compact = DisplayMode switch
        {
            CalendarDisplayMode.Compact => true,
            CalendarDisplayMode.Regular => false,
            _ => ActualWidth > 0 && ActualWidth < CompactMaxWidth,
        };
        var changed = compact != _isCompact;
        _isCompact = compact;
        VisualStateManager.GoToState(this, compact ? "Compact" : "Regular", false);
        if (changed)
        {
            RefreshDayStates(false);
            UpdateHeader();
            UpdateAgenda(false);
        }
    }

    private static Storyboard Animate(DependencyObject target, string property, double to, TimeSpan duration, KeySpline spline, TimeSpan delay, Action completed)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(Keyframe(target, property, to, duration, spline, delay));
        storyboard.Completed += (_, _) =>
        {
            storyboard.Stop();
            completed();
        };
        storyboard.Begin();
        return storyboard;
    }

    private static DoubleAnimationUsingKeyFrames Keyframe(DependencyObject target, string property, double to, TimeSpan duration, KeySpline spline, TimeSpan delay)
    {
        var animation = new DoubleAnimationUsingKeyFrames { BeginTime = delay, EnableDependentAnimation = true };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = duration, Value = to, KeySpline = spline });
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    #endregion
}
