using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;

namespace HorizontalCalendar.Controls;

/// <summary>
/// One day of the <see cref="HorizontalCalendar"/> strip: weekday, day number, one tick per event (up to four) or the
/// unavailable label. The calendar sets its properties; the look is in the <c>CalendarDayButton</c> style.
/// </summary>
[TemplatePart(Name = PART_Ticks, Type = typeof(Panel))]
[TemplatePart(Name = PART_Hatch, Type = typeof(Path))]
[TemplateVisualState(GroupName = "SelectionStates", Name = "Unselected")]
[TemplateVisualState(GroupName = "SelectionStates", Name = "Selected")]
[TemplateVisualState(GroupName = "TodayStates", Name = "NotToday")]
[TemplateVisualState(GroupName = "TodayStates", Name = "Today")]
[TemplateVisualState(GroupName = "TodayStates", Name = "TodaySelected")]
[TemplateVisualState(GroupName = "AvailabilityStates", Name = "Available")]
[TemplateVisualState(GroupName = "AvailabilityStates", Name = "Past")]
[TemplateVisualState(GroupName = "AvailabilityStates", Name = "Unavailable")]
[TemplateVisualState(GroupName = "SizeStates", Name = "Regular")]
[TemplateVisualState(GroupName = "SizeStates", Name = "Compact")]
public partial class CalendarDayButton : Button
{
    private const string PART_Ticks = "PART_Ticks";
    private const string PART_Hatch = "PART_Hatch";
    private const int MaxTicks = 4;

    private Panel? _ticks;
    private Path? _hatch;
    private bool _isPointerOver;

    public CalendarDayButton()
    {
        DefaultStyleKey = typeof(CalendarDayButton);
        SizeChanged += (_, e) => UpdateHatch(e.NewSize);
    }

    public DateTime Date { get; internal set; }

    internal string Weekday { get; set; } = string.Empty;

    internal string DayNumber { get; set; } = string.Empty;

    internal int TickCount { get; set; }

    internal bool ShowTicks { get; set; } = true;

    internal bool IsSelectedDay { get; set; }

    internal bool IsToday { get; set; }

    internal bool IsPast { get; set; }

    internal string? UnavailableLabel { get; set; }

    internal bool IsCompact { get; set; }

    internal bool IsUnavailable => UnavailableLabel != null;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ticks = GetTemplateChild(PART_Ticks) as Panel;
        _hatch = GetTemplateChild(PART_Hatch) as Path;
        Refresh(false);
    }

    /// <summary>Pushes the day's values into the template and moves it to the matching visual states.</summary>
    internal void Refresh(bool useTransitions)
    {
        if (GetTemplateChild("WeekdayText") is TextBlock weekday) weekday.Text = Weekday;
        if (GetTemplateChild("NumberText") is TextBlock number) number.Text = DayNumber;
        if (GetTemplateChild("UnavailableText") is TextBlock label) label.Text = UnavailableLabel?.ToUpperInvariant() ?? string.Empty;

        if (_ticks != null)
        {
            var count = IsUnavailable ? 0 : Math.Min(MaxTicks, TickCount);
            for (int i = 0; i < _ticks.Children.Count; i++)
            {
                _ticks.Children[i].Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
            }
            _ticks.Opacity = ShowTicks ? 1 : 0;
        }

        VisualStateManager.GoToState(this, IsCompact ? "Compact" : "Regular", false);
        VisualStateManager.GoToState(this, IsSelectedDay ? "Selected" : "Unselected", useTransitions);
        VisualStateManager.GoToState(this, !IsToday ? "NotToday" : IsSelectedDay ? "TodaySelected" : "Today", useTransitions);
        VisualStateManager.GoToState(this, IsUnavailable ? "Unavailable" : IsPast && !IsSelectedDay ? "Past" : "Available", useTransitions);
        UpdatePointerState(useTransitions);
    }

    protected override void OnPointerEntered(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        UpdatePointerState(true);
    }

    protected override void OnPointerExited(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        UpdatePointerState(true);
    }

    private void UpdatePointerState(bool useTransitions)
    {
        var state = !_isPointerOver || IsSelectedDay ? "Rest" : IsUnavailable ? "HatchHover" : "Hover";
        VisualStateManager.GoToState(this, state, useTransitions);
    }

    /// <summary>135° hatching: a 1 px line every 6 px across the gradient, like the sheet's repeating gradient.</summary>
    private void UpdateHatch(Size size)
    {
        if (_hatch == null || size.Width <= 0 || size.Height <= 0) return;
        const double step = 8.5; // 6 px period along the 135° axis, measured horizontally
        var geometry = new PathGeometry();
        for (double x = -size.Height; x < size.Width; x += step)
        {
            var figure = new PathFigure { StartPoint = new Point(x, size.Height), IsClosed = false, IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(x + size.Height, 0) });
            geometry.Figures.Add(figure);
        }
        _hatch.Data = geometry;
    }
}
