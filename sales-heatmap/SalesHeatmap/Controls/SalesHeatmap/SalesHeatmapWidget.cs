using System.Globalization;
using System.Windows.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace SalesHeatmap.Controls;

/// <summary>One row of the top-regions list, computed for the selected period.</summary>
public sealed record SalesRegionRow(string Number, string Name, string ValueText, string DeltaText, Visibility UpVisibility, Visibility DownVisibility);

/// <summary>
/// The sales overview card: a 14-week daily revenue heatmap of one quarter in five steps of one hue, a period
/// selector (the quarter's three months and the quarter), the period's revenue and change, the selected day's
/// figures and the top regions. Days can be hovered, clicked or reached with the arrow keys (↑ ↓ a day, ← → a week).
/// The look is the default style in Themes/SalesHeatmap.xaml.
/// </summary>
[TemplatePart(Name = GridHostPart, Type = typeof(Control))]
[TemplatePart(Name = GridCanvasPart, Type = typeof(Canvas))]
[TemplatePart(Name = TooltipPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = RetryPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ClearFiltersPart, Type = typeof(ButtonBase))]
[TemplateVisualState(GroupName = "DataStates", Name = "Loading")]
[TemplateVisualState(GroupName = "DataStates", Name = "Ready")]
[TemplateVisualState(GroupName = "DataStates", Name = "Empty")]
[TemplateVisualState(GroupName = "DataStates", Name = "Stale")]
[TemplateVisualState(GroupName = "DeltaStates", Name = "DeltaUp")]
[TemplateVisualState(GroupName = "DeltaStates", Name = "DeltaDown")]
[TemplateVisualState(GroupName = "DeltaStates", Name = "DeltaNone")]
[TemplateVisualState(GroupName = "LayoutStates", Name = "Regular")]
[TemplateVisualState(GroupName = "LayoutStates", Name = "Compact")]
public partial class SalesHeatmapWidget : Control
{
    private const string GridHostPart = "PART_GridHost";
    private const string GridCanvasPart = "PART_GridCanvas";
    private const string TooltipPart = "PART_Tooltip";
    private const string RetryPart = "PART_RetryButton";
    private const string ClearFiltersPart = "PART_ClearFiltersButton";

    /// <summary>Below this card width the card stacks for a phone: figures, grid, day, regions.</summary>
    public const double CompactWidth = 860;

    private const int Rows = 7;
    private const int Columns = 14;
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private readonly SalesHeatmapCell[] _cells = new SalesHeatmapCell[Rows * Columns];
    private readonly List<TextBlock> _axisLabels = [];
    private readonly List<ToggleButton> _periodButtons = [];
    private Control? _gridHost;
    private Canvas? _canvas;
    private FrameworkElement? _tooltip;
    private ButtonBase? _retry;
    private ButtonBase? _clear;
    private int _hover = -1;
    private bool _isCompact;
    private bool _syncingPeriod;
    private double _left, _top, _pitch, _cell;

    public SalesHeatmapWidget()
    {
        DefaultStyleKey = typeof(SalesHeatmapWidget);
        SizeChanged += (_, e) => UpdateLayoutState(e.NewSize.Width, useTransitions: true);
    }

    #region Dependency properties

    public static readonly DependencyProperty SnapshotProperty = DependencyProperty.Register(
        nameof(Snapshot), typeof(SalesSnapshot), typeof(SalesHeatmapWidget), new PropertyMetadata(null, OnSnapshotChanged));

    /// <summary>The data: days, quarter, today, region shares and comparisons.</summary>
    public SalesSnapshot? Snapshot { get => (SalesSnapshot?)GetValue(SnapshotProperty); set => SetValue(SnapshotProperty, value); }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(SalesHeatmapState), typeof(SalesHeatmapWidget), new PropertyMetadata(SalesHeatmapState.Loading, OnRefreshNeeded));

    /// <summary>Loading, Ready, Empty (no sales in the filter) or Stale (the last refresh failed).</summary>
    public SalesHeatmapState State { get => (SalesHeatmapState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public static readonly DependencyProperty PeriodIndexProperty = DependencyProperty.Register(
        nameof(PeriodIndex), typeof(int), typeof(SalesHeatmapWidget), new PropertyMetadata(-1, OnPeriodChanged));

    /// <summary>0–2 the quarter's months, 3 the whole quarter; -1 (default) picks today's month.</summary>
    public int PeriodIndex { get => (int)GetValue(PeriodIndexProperty); set => SetValue(PeriodIndexProperty, value); }

    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(SalesHeatmapWidget), new PropertyMetadata(null, OnRefreshNeeded));

    /// <summary>The day the detail panel shows; null picks the last day with data before today.</summary>
    public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    public static readonly DependencyProperty ThresholdsProperty = DependencyProperty.Register(
        nameof(Thresholds), typeof(DoubleCollection), typeof(SalesHeatmapWidget), new PropertyMetadata(null, OnRefreshNeeded));

    /// <summary>The four revenue steps between the five colour levels; default 4000, 7000, 10000, 13000.</summary>
    public DoubleCollection? Thresholds { get => (DoubleCollection?)GetValue(ThresholdsProperty); set => SetValue(ThresholdsProperty, value); }

    public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
        nameof(FilterText), typeof(string), typeof(SalesHeatmapWidget), new PropertyMetadata(string.Empty));

    /// <summary>The active filter, shown in the header (e.g. "Region: Canada · Online").</summary>
    public string FilterText { get => (string)GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand), typeof(ICommand), typeof(SalesHeatmapWidget), new PropertyMetadata(null));

    /// <summary>Runs when Retry (shown in <see cref="SalesHeatmapState.Stale"/>) is pressed.</summary>
    public ICommand? RetryCommand { get => (ICommand?)GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }

    public static readonly DependencyProperty ClearFiltersCommandProperty = DependencyProperty.Register(
        nameof(ClearFiltersCommand), typeof(ICommand), typeof(SalesHeatmapWidget), new PropertyMetadata(null));

    /// <summary>Runs when Clear filters (shown in <see cref="SalesHeatmapState.Empty"/>) is pressed.</summary>
    public ICommand? ClearFiltersCommand { get => (ICommand?)GetValue(ClearFiltersCommandProperty); set => SetValue(ClearFiltersCommandProperty, value); }

    /// <summary>Raised when Retry is pressed.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Raised when Clear filters is pressed.</summary>
    public event EventHandler? ClearFiltersRequested;

    // Read-only values the template shows.
    public static readonly DependencyProperty SubtitleProperty = Text(nameof(Subtitle));
    public string Subtitle { get => (string)GetValue(SubtitleProperty); private set => SetValue(SubtitleProperty, value); }

    public static readonly DependencyProperty PeriodTitleProperty = Text(nameof(PeriodTitle));
    public string PeriodTitle { get => (string)GetValue(PeriodTitleProperty); private set => SetValue(PeriodTitleProperty, value); }

    public static readonly DependencyProperty RegionsTitleProperty = Text(nameof(RegionsTitle));
    public string RegionsTitle { get => (string)GetValue(RegionsTitleProperty); private set => SetValue(RegionsTitleProperty, value); }

    public static readonly DependencyProperty TotalTextProperty = Text(nameof(TotalText));
    public string TotalText { get => (string)GetValue(TotalTextProperty); private set => SetValue(TotalTextProperty, value); }

    public static readonly DependencyProperty DeltaTextProperty = Text(nameof(DeltaText));
    public string DeltaText { get => (string)GetValue(DeltaTextProperty); private set => SetValue(DeltaTextProperty, value); }

    public static readonly DependencyProperty DeltaNoteProperty = Text(nameof(DeltaNote));
    public string DeltaNote { get => (string)GetValue(DeltaNoteProperty); private set => SetValue(DeltaNoteProperty, value); }

    public static readonly DependencyProperty SelectedDateTextProperty = Text(nameof(SelectedDateText));
    public string SelectedDateText { get => (string)GetValue(SelectedDateTextProperty); private set => SetValue(SelectedDateTextProperty, value); }

    public static readonly DependencyProperty SelectedShortDateTextProperty = Text(nameof(SelectedShortDateText));
    public string SelectedShortDateText { get => (string)GetValue(SelectedShortDateTextProperty); private set => SetValue(SelectedShortDateTextProperty, value); }

    public static readonly DependencyProperty SelectedRevenueTextProperty = Text(nameof(SelectedRevenueText));
    public string SelectedRevenueText { get => (string)GetValue(SelectedRevenueTextProperty); private set => SetValue(SelectedRevenueTextProperty, value); }

    public static readonly DependencyProperty SelectedOrdersTextProperty = Text(nameof(SelectedOrdersText));
    public string SelectedOrdersText { get => (string)GetValue(SelectedOrdersTextProperty); private set => SetValue(SelectedOrdersTextProperty, value); }

    public static readonly DependencyProperty SelectedVsTextProperty = Text(nameof(SelectedVsText));
    public string SelectedVsText { get => (string)GetValue(SelectedVsTextProperty); private set => SetValue(SelectedVsTextProperty, value); }

    public static readonly DependencyProperty UpdatedTextProperty = Text(nameof(UpdatedText));
    public string UpdatedText { get => (string)GetValue(UpdatedTextProperty); private set => SetValue(UpdatedTextProperty, value); }

    public static readonly DependencyProperty StaleTextProperty = Text(nameof(StaleText));
    public string StaleText { get => (string)GetValue(StaleTextProperty); private set => SetValue(StaleTextProperty, value); }

    public static readonly DependencyProperty TooltipDateProperty = Text(nameof(TooltipDate));
    public string TooltipDate { get => (string)GetValue(TooltipDateProperty); private set => SetValue(TooltipDateProperty, value); }

    public static readonly DependencyProperty TooltipValueProperty = Text(nameof(TooltipValue));
    public string TooltipValue { get => (string)GetValue(TooltipValueProperty); private set => SetValue(TooltipValueProperty, value); }

    public static readonly DependencyProperty ScaleLabelsProperty = DependencyProperty.Register(
        nameof(ScaleLabels), typeof(IList<string>), typeof(SalesHeatmapWidget), new PropertyMetadata(Array.Empty<string>()));
    public IList<string> ScaleLabels { get => (IList<string>)GetValue(ScaleLabelsProperty); private set => SetValue(ScaleLabelsProperty, value); }

    public static readonly DependencyProperty RegionRowsProperty = DependencyProperty.Register(
        nameof(RegionRows), typeof(IList<SalesRegionRow>), typeof(SalesHeatmapWidget), new PropertyMetadata(Array.Empty<SalesRegionRow>()));
    public IList<SalesRegionRow> RegionRows { get => (IList<SalesRegionRow>)GetValue(RegionRowsProperty); private set => SetValue(RegionRowsProperty, value); }

    private static DependencyProperty Text(string name)
        => DependencyProperty.Register(name, typeof(string), typeof(SalesHeatmapWidget), new PropertyMetadata(string.Empty));

    #endregion

    protected override void OnApplyTemplate()
    {
        if (_gridHost is not null)
        {
            _gridHost.KeyDown -= OnGridKeyDown;
            _gridHost.SizeChanged -= OnGridHostSizeChanged;
            _gridHost.PointerExited -= OnGridPointerExited;
        }

        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }

        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }

        foreach (var b in _periodButtons)
        {
            b.Checked -= OnPeriodButtonChecked;
        }

        _periodButtons.Clear();
        base.OnApplyTemplate();

        _gridHost = GetTemplateChild(GridHostPart) as Control;
        _canvas = GetTemplateChild(GridCanvasPart) as Canvas;
        _tooltip = GetTemplateChild(TooltipPart) as FrameworkElement;
        _retry = GetTemplateChild(RetryPart) as ButtonBase;
        _clear = GetTemplateChild(ClearFiltersPart) as ButtonBase;

        if (_gridHost is not null)
        {
            _gridHost.KeyDown += OnGridKeyDown;
            _gridHost.SizeChanged += OnGridHostSizeChanged;
            _gridHost.PointerExited += OnGridPointerExited;
        }

        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }

        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }

        foreach (var prefix in new[] { "PART_Period", "PART_CompactPeriod" })
        {
            for (var i = 0; i < 4; i++)
            {
                if (GetTemplateChild(prefix + i) is ToggleButton t)
                {
                    t.Tag = i;
                    t.Checked += OnPeriodButtonChecked;
                    _periodButtons.Add(t);
                }
            }
        }

        BuildGrid();
        UpdateLayoutState(ActualWidth, useTransitions: false);
        Refresh(useTransitions: false);
    }

    #region Callbacks and input

    private static void OnSnapshotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var w = (SalesHeatmapWidget)d;
        w.LayoutCells();
        w.Refresh(useTransitions: true);
    }

    private static void OnRefreshNeeded(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SalesHeatmapWidget)d).Refresh(useTransitions: true);

    private static void OnPeriodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SalesHeatmapWidget)d).Refresh(useTransitions: true);

    private void OnPeriodButtonChecked(object sender, RoutedEventArgs e)
    {
        if (!_syncingPeriod && sender is ToggleButton { Tag: int index })
        {
            PeriodIndex = index;
        }
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
        if (RetryCommand?.CanExecute(null) == true)
        {
            RetryCommand.Execute(null);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ClearFiltersRequested?.Invoke(this, EventArgs.Empty);
        if (ClearFiltersCommand?.CanExecute(null) == true)
        {
            ClearFiltersCommand.Execute(null);
        }
    }

    private void OnGridHostSizeChanged(object sender, SizeChangedEventArgs e) => LayoutCells();

    private void OnGridPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hover = -1;
        UpdateTooltip();
    }

    private void OnCellPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is SalesHeatmapCell { Day.Kind: not SalesDayKind.Void } cell && State is SalesHeatmapState.Ready or SalesHeatmapState.Stale)
        {
            _hover = cell.Index;
            UpdateTooltip();
        }
    }

    private void OnCellTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is SalesHeatmapCell { IsInteractive: true, Day: { } day } && State is SalesHeatmapState.Ready or SalesHeatmapState.Stale)
        {
            SelectedDate = day.Date;
            _gridHost?.Focus(FocusState.Pointer);
        }
    }

    private void OnGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (Snapshot is not { } s || State is not (SalesHeatmapState.Ready or SalesHeatmapState.Stale))
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            _hover = -1;
            UpdateTooltip();
            e.Handled = true;
            return;
        }

        var move = e.Key switch
        {
            VirtualKey.Up => -1,
            VirtualKey.Down => 1,
            VirtualKey.Left => -Rows,
            VirtualKey.Right => Rows,
            _ => 0,
        };
        if (move == 0)
        {
            return;
        }

        e.Handled = true;
        var j = SelectedIndex(s) + move;
        while (j >= 0 && j < s.Days.Count && s.Days[j].Kind is SalesDayKind.Void or SalesDayKind.Ahead)
        {
            j += move;
        }

        if (j >= 0 && j < s.Days.Count)
        {
            SelectedDate = s.Days[j].Date;
            _hover = j;
            UpdateTooltip();
        }
    }

    #endregion

    #region Grid

    private void BuildGrid()
    {
        if (_canvas is null)
        {
            return;
        }

        foreach (var c in _cells)
        {
            if (c is not null)
            {
                c.PointerEntered -= OnCellPointerEntered;
                c.Tapped -= OnCellTapped;
            }
        }

        _canvas.Children.Clear();
        _axisLabels.Clear();
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = new SalesHeatmapCell { Index = i };
            cell.PointerEntered += OnCellPointerEntered;
            cell.Tapped += OnCellTapped;
            _cells[i] = cell;
            _canvas.Children.Add(cell);
        }

        // Three month labels and Mon / Wed / Fri.
        for (var i = 0; i < 6; i++)
        {
            var label = new TextBlock();
            if (Application.Current.Resources.TryGetValue("SalesHeatmapAxisLabelStyle", out var style) && style is Style s)
            {
                label.Style = s;
            }

            _axisLabels.Add(label);
            _canvas.Children.Add(label);
        }

        if (_tooltip is not null && _tooltip.Parent is Panel p && p != _canvas)
        {
            // The tooltip lives in the template next to the canvas; nothing to move.
            _ = p;
        }

        LayoutCells();
    }

    private void LayoutCells()
    {
        if (_canvas is null || _gridHost is null || _cells[0] is null)
        {
            return;
        }

        // Regular: the prototype's 30 px cells on a 36 px pitch. Compact: the pitch fills the width (23 px at 390).
        var gap = _isCompact ? 4d : 6d;
        _left = _isCompact ? 28 : 34;
        _top = _isCompact ? 16 : 20;
        _pitch = _isCompact
            ? Math.Clamp((_gridHost.ActualWidth - _left + gap) / Columns, 16, 26)
            : 36;
        _cell = _pitch - gap;
        var radius = _isCompact ? Math.Round(_cell * 0.22, 1) : 7;

        _canvas.Width = _left + Columns * _pitch - gap;
        _canvas.Height = _top + Rows * _pitch - gap;
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            cell.Width = _cell;
            cell.Height = _cell;
            cell.CornerRadius = new CornerRadius(radius);
            Canvas.SetLeft(cell, _left + i / Rows * _pitch);
            Canvas.SetTop(cell, _top + i % Rows * _pitch);
        }

        var s = Snapshot;
        var month = 0;
        for (var m = 0; m < 3; m++)
        {
            var label = _axisLabels[m];
            label.Text = s is null ? string.Empty : s.QuarterStart.AddMonths(m).ToString("MMM", Us);
            var firstColumn = s is null ? m * 4 + 1 : FirstColumnOf(s, s.QuarterStart.AddMonths(m).Month);
            Canvas.SetLeft(label, _left + firstColumn * _pitch);
            Canvas.SetTop(label, _isCompact ? -2 : 0);
            month++;
        }

        string[] weekdays = ["Mon", "Wed", "Fri"];
        for (var w = 0; w < 3; w++)
        {
            var label = _axisLabels[3 + w];
            label.Text = weekdays[w];
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, _top + w * 2 * _pitch + _cell / 2 - 8);
        }

        UpdateTooltip();
    }

    private static int FirstColumnOf(SalesSnapshot s, int month)
    {
        for (var i = 0; i < s.Days.Count; i++)
        {
            if (s.Days[i].Date.Month == month && s.Days[i].Kind != SalesDayKind.Void)
            {
                return i / Rows;
            }
        }

        return 0;
    }

    private void UpdateTooltip()
    {
        if (_tooltip is null)
        {
            return;
        }

        var s = Snapshot;
        if (s is null || _hover < 0 || _hover >= s.Days.Count || s.Days[_hover].Kind == SalesDayKind.Void
            || State is not (SalesHeatmapState.Ready or SalesHeatmapState.Stale))
        {
            _tooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var day = s.Days[_hover];
        TooltipDate = day.Date.ToString("ddd, MMM d", Us);
        TooltipValue = day.Kind switch
        {
            SalesDayKind.Data => Money(day.Revenue ?? 0) + " · " + day.Orders.ToString(Us) + " orders",
            SalesDayKind.Closed => "Closed · no sales",
            SalesDayKind.NoData => "No data recorded",
            _ => "Not yet",
        };

        _tooltip.Visibility = Visibility.Visible;
        _tooltip.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _tooltip.DesiredSize;
        var x = _left + _hover / Rows * _pitch + _cell / 2 - size.Width / 2;
        var y = _top + _hover % Rows * _pitch - size.Height - 10;
        _tooltip.RenderTransform = new TranslateTransform { X = x, Y = y };
    }

    #endregion

    #region State to texts

    private int ResolvedPeriod(SalesSnapshot s)
    {
        if (PeriodIndex is >= 0 and <= 3)
        {
            return PeriodIndex;
        }

        var months = (s.Today.Year - s.QuarterStart.Year) * 12 + s.Today.Month - s.QuarterStart.Month;
        return months is >= 0 and <= 2 ? months : 3;
    }

    private static bool InPeriod(SalesSnapshot s, SalesDay day, int period)
        => day.Kind != SalesDayKind.Void && (period == 3 || day.Date.Month == s.QuarterStart.AddMonths(period).Month);

    private int SelectedIndex(SalesSnapshot s)
    {
        if (SelectedDate is { } d)
        {
            for (var i = 0; i < s.Days.Count; i++)
            {
                if (s.Days[i].Date.Date == d.Date)
                {
                    return i;
                }
            }
        }

        for (var i = s.Days.Count - 1; i >= 0; i--)
        {
            if (s.Days[i].Kind == SalesDayKind.Data && s.Days[i].Date < s.Today)
            {
                return i;
            }
        }

        return 0;
    }

    private void Refresh(bool useTransitions)
    {
        var s = Snapshot;
        var state = s is null ? SalesHeatmapState.Loading : State;

        VisualStateManager.GoToState(this, state.ToString(), useTransitions);

        var thresholds = Thresholds is { Count: 4 } t ? t.ToArray() : [4000d, 7000d, 10000d, 13000d];
        ScaleLabels = [.. thresholds.Select((v, i) => "$" + (v / 1000).ToString("0.#", Us) + "k" + (i == 3 ? "+" : string.Empty))];

        if (s is null)
        {
            ApplyLoadingTexts();
            foreach (var b in _periodButtons)
            {
                b.Content = "—";
                b.IsChecked = false;
            }

            foreach (var cell in _cells)
            {
                cell?.Apply("Skeleton", true, false, useTransitions);
            }

            VisualStateManager.GoToState(this, "DeltaNone", useTransitions);
            return;
        }

        var period = ResolvedPeriod(s);
        SyncPeriodButtons(period);
        var quarter = (s.QuarterStart.Month - 1) / 3 + 1;
        Subtitle = $"Daily revenue · Q{quarter} {s.QuarterStart.Year} · today is {s.Today.ToString("MMM d", Us)}";
        UpdatedText = "Updated " + s.UpdatedAt.ToLocalTime().ToString("h:mm tt", Us);
        StaleText = "Showing data from " + s.UpdatedAt.ToLocalTime().ToString("h:mm tt", Us);

        var monthDate = s.QuarterStart.AddMonths(Math.Min(period, 2));
        var toDate = period == 3 || (monthDate.Month == s.Today.Month && monthDate.Year == s.Today.Year);
        // Labels are uppercase in the design (the prototype's text-transform), authored so for Skia.
        var periodName = ((period == 3 ? $"Q{quarter}" : monthDate.ToString("MMMM", Us)) + (toDate ? " to date" : string.Empty)).ToUpperInvariant();
        PeriodTitle = "REVENUE · " + periodName;
        RegionsTitle = "TOP REGIONS · " + periodName;

        var selected = SelectedIndex(s);
        var periodDays = s.Days.Where(d => InPeriod(s, d, period)).ToArray();
        var total = periodDays.Where(d => d.Revenue is not null).Sum(d => d.Revenue!.Value);
        var dataDays = periodDays.Where(d => d.Kind == SalesDayKind.Data).ToArray();
        var average = dataDays.Length > 0 ? total / dataDays.Length : 0;

        if (state == SalesHeatmapState.Loading)
        {
            ApplyLoadingTexts();
        }
        else if (state == SalesHeatmapState.Empty)
        {
            // No sales in the filter: say so, and never show zeros.
            TotalText = "—";
            DeltaText = string.Empty;
            DeltaNote = string.Empty;
            VisualStateManager.GoToState(this, "DeltaNone", useTransitions);
            SelectedDateText = SelectedShortDateText = "No day selected";
            SelectedRevenueText = SelectedOrdersText = SelectedVsText = "—";
            RegionRows = [.. s.Regions.Take(3).Select((r, i) => new SalesRegionRow((i + 1).ToString("00", Us), r.Name, "—", string.Empty, Visibility.Collapsed, Visibility.Collapsed))];
        }
        else
        {
            TotalText = total > 0 ? Money(total) : "—";
            var comparison = period < s.Comparisons.Count ? s.Comparisons[period] : null;
            if (comparison?.Change is { } change)
            {
                DeltaText = Signed(change, "0.0") + "%";
                VisualStateManager.GoToState(this, change >= 0 ? "DeltaUp" : "DeltaDown", useTransitions);
            }
            else
            {
                DeltaText = string.Empty;
                VisualStateManager.GoToState(this, "DeltaNone", useTransitions);
            }

            DeltaNote = comparison?.Note ?? string.Empty;

            var day = s.Days[selected];
            SelectedDateText = day.Date.ToString("dddd, MMMM d", Us);
            SelectedShortDateText = day.Date.ToString("ddd, MMM d", Us).ToUpperInvariant();
            SelectedRevenueText = day.Revenue is { } rev ? Money(rev) : "—";
            SelectedOrdersText = day.Kind switch
            {
                SalesDayKind.Data => day.Orders.ToString(Us),
                SalesDayKind.Closed => "0",
                _ => "—",
            };
            SelectedVsText = day.Kind switch
            {
                SalesDayKind.Data when average > 0 => Signed((day.Revenue!.Value - average) / average * 100, "0.0") + "%",
                SalesDayKind.Closed => "Closed",
                _ => "No data",
            };

            RegionRows = [.. s.Regions.Take(3).Select((r, i) => new SalesRegionRow(
                (i + 1).ToString("00", Us),
                r.Name,
                total > 0 ? Money(total * r.Share) : "—",
                total > 0 ? Signed(r.Change, "0.0") + "%" : string.Empty,
                total > 0 && r.Change >= 0 ? Visibility.Visible : Visibility.Collapsed,
                total > 0 && r.Change < 0 ? Visibility.Visible : Visibility.Collapsed))];
        }

        for (var i = 0; i < _cells.Length && i < s.Days.Count; i++)
        {
            var cell = _cells[i];
            if (cell is null)
            {
                continue;
            }

            var day = s.Days[i];
            cell.Day = day;
            var kind = state is SalesHeatmapState.Loading or SalesHeatmapState.Empty
                ? (day.Kind == SalesDayKind.Void ? "Void" : "Skeleton")
                : day.Kind switch
                {
                    SalesDayKind.Data => "Level" + (1 + thresholds.Count(th => day.Revenue >= th)),
                    SalesDayKind.Closed => "Closed",
                    SalesDayKind.NoData => "NoData",
                    SalesDayKind.Ahead => "Ahead",
                    _ => "Void",
                };
            var inPeriod = state is SalesHeatmapState.Loading or SalesHeatmapState.Empty || day.Kind == SalesDayKind.Void || InPeriod(s, day, period);
            var isSelected = i == selected && state is SalesHeatmapState.Ready or SalesHeatmapState.Stale;
            cell.Apply(kind, inPeriod, isSelected, useTransitions);
        }

        if (_gridHost is not null)
        {
            AutomationProperties.SetName(_gridHost, "Daily revenue grid. Arrow keys move the selection. Selected: "
                + SelectedDateText + ", " + SelectedRevenueText);
        }

        UpdateTooltip();
    }

    private void ApplyLoadingTexts()
    {
        PeriodTitle = string.IsNullOrEmpty(PeriodTitle) ? "REVENUE" : PeriodTitle;
        RegionsTitle = string.IsNullOrEmpty(RegionsTitle) ? "TOP REGIONS" : RegionsTitle;
        TotalText = "$—";
        DeltaText = DeltaNote = string.Empty;
        SelectedDateText = SelectedShortDateText = " ";
        SelectedRevenueText = SelectedOrdersText = SelectedVsText = "—";
        RegionRows = [.. Enumerable.Range(1, 3).Select(i => new SalesRegionRow(i.ToString("00", Us), " ", " ", string.Empty, Visibility.Collapsed, Visibility.Collapsed))];
    }

    private void SyncPeriodButtons(int period)
    {
        _syncingPeriod = true;
        foreach (var b in _periodButtons)
        {
            if (b.Tag is int i)
            {
                b.IsChecked = i == period;
                if (Snapshot is { } s)
                {
                    b.Content = i == 3 ? $"Q{(s.QuarterStart.Month - 1) / 3 + 1}" : s.QuarterStart.AddMonths(i).ToString("MMM", Us);
                }
            }
        }

        _syncingPeriod = false;
    }

    private void UpdateLayoutState(double width, bool useTransitions)
    {
        if (width <= 0 || double.IsNaN(width))
        {
            return;
        }

        var compact = width < CompactWidth;
        if (compact == _isCompact && _cells[0] is not null && _gridHost is not null)
        {
            return;
        }

        _isCompact = compact;
        VisualStateManager.GoToState(this, compact ? "Compact" : "Regular", useTransitions);
        LayoutCells();
    }

    internal static string Money(double value) => "$" + Math.Round(value).ToString("#,0", Us);

    /// <summary>"+14.5" or "−5.0": a real minus sign, so the sign reads with the arrow and the colour.</summary>
    private static string Signed(double value, string format)
        => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString(format, Us);

    #endregion
}
