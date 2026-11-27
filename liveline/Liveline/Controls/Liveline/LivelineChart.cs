using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Liveline.Models;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Liveline;

/// <summary>
/// A live metric card: the latest value with its change, a stream status chip, a window selector (1, 5 or 15 min),
/// pause and resume, a fixed-axis line chart with an alert threshold, hover and keyboard inspection, and min, avg and
/// max for the window. The look is the default style in LivelineChart.xaml; the plot is drawn by
/// <see cref="LivelineChartCanvas"/> with SkiaSharp.
/// </summary>
/// <remarks>
/// Feed it with <see cref="Data"/> (one sample a second, newest last; an <see cref="INotifyCollectionChanged"/> list
/// is observed) and drive <see cref="State"/>. Pause and Resume are handled by the control: Pause waits for the next
/// sample, then stops and stamps <see cref="PausedTime"/>.
/// </remarks>
[TemplatePart(Name = CanvasPart, Type = typeof(LivelineChartCanvas))]
[TemplatePart(Name = PlotPart, Type = typeof(Control))]
[TemplatePart(Name = PausePart, Type = typeof(ButtonBase))]
[TemplatePart(Name = CompactPausePart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ReconnectPart, Type = typeof(ButtonBase))]
[TemplateVisualState(GroupName = "StreamStates", Name = "Connecting")]
[TemplateVisualState(GroupName = "StreamStates", Name = "Live")]
[TemplateVisualState(GroupName = "StreamStates", Name = "Pausing")]
[TemplateVisualState(GroupName = "StreamStates", Name = "Paused")]
[TemplateVisualState(GroupName = "StreamStates", Name = "Disconnected")]
[TemplateVisualState(GroupName = "LayoutStates", Name = "Regular")]
[TemplateVisualState(GroupName = "LayoutStates", Name = "Compact")]
public partial class LivelineChart : Control
{
    private const string CanvasPart = "PART_Canvas";
    private const string PlotPart = "PART_Plot";
    private const string PausePart = "PART_PauseButton";
    private const string CompactPausePart = "PART_CompactPauseButton";
    private const string ReconnectPart = "PART_ReconnectButton";

    /// <summary>Below this width the card stacks: chart first, controls low (the prototype's phone layout).</summary>
    public const double CompactWidth = 560;

    private static readonly TimeSpan[] Windows = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _frameTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private LivelineChartCanvas? _canvas;
    private Control? _plot;
    private ButtonBase? _pause;
    private ButtonBase? _compactPause;
    private ButtonBase? _reconnect;
    private readonly List<ToggleButton> _windowButtons = [];
    private INotifyCollectionChanged? _observed;
    private LivelinePoint[] _frozen = [];
    private double _arrivalMs;
    private double _disconnectedMs;
    private int? _inspect;
    private bool _isCompact;
    private bool _syncingWindow;

    public LivelineChart()
    {
        DefaultStyleKey = typeof(LivelineChart);
        _frameTimer.Tick += (_, _) => OnFrame();
        Loaded += (_, _) => UpdateTimer();
        Unloaded += (_, _) => _frameTimer.Stop();
        SizeChanged += (_, e) => UpdateLayoutState(e.NewSize.Width, useTransitions: true);
        // Theme brushes can change colour in place without a property change: read them again.
        ActualThemeChanged += (_, _) => PushColors();
    }

    #region Dependency properties

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(LivelineChart), new PropertyMetadata(string.Empty));

    /// <summary>The metric's name, e.g. "API latency · p95".</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(LivelineChart), new PropertyMetadata("ms", OnPlotPropertyChanged));

    /// <summary>The unit after the value and on the top axis label. Default "ms".</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IList<LivelinePoint>), typeof(LivelineChart), new PropertyMetadata(null, OnDataChanged));

    /// <summary>The samples, oldest first. A list that raises CollectionChanged is observed; otherwise assign a new list per sample.</summary>
    public IList<LivelinePoint>? Data { get => (IList<LivelinePoint>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(LivelineState), typeof(LivelineChart), new PropertyMetadata(LivelineState.Connecting, OnStateChanged));

    /// <summary>The stream state. The control moves Live → Pausing → Paused → Live itself from its pause button.</summary>
    public LivelineState State { get => (LivelineState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window), typeof(TimeSpan), typeof(LivelineChart), new PropertyMetadata(TimeSpan.FromMinutes(1), OnWindowChanged));

    /// <summary>The time span plotted: 1, 5 or 15 minutes from the selector, or any span set in code.</summary>
    public TimeSpan Window { get => (TimeSpan)GetValue(WindowProperty); set => SetValue(WindowProperty, value); }

    public static readonly DependencyProperty AxisMinimumProperty = DependencyProperty.Register(
        nameof(AxisMinimum), typeof(double), typeof(LivelineChart), new PropertyMetadata(40d, OnPlotPropertyChanged));

    /// <summary>The fixed bottom of the value axis. Default 40.</summary>
    public double AxisMinimum { get => (double)GetValue(AxisMinimumProperty); set => SetValue(AxisMinimumProperty, value); }

    public static readonly DependencyProperty AxisMaximumProperty = DependencyProperty.Register(
        nameof(AxisMaximum), typeof(double), typeof(LivelineChart), new PropertyMetadata(70d, OnPlotPropertyChanged));

    /// <summary>The fixed top of the value axis. Default 70. The axis never rescales to the data.</summary>
    public double AxisMaximum { get => (double)GetValue(AxisMaximumProperty); set => SetValue(AxisMaximumProperty, value); }

    public static readonly DependencyProperty AxisStepProperty = DependencyProperty.Register(
        nameof(AxisStep), typeof(double), typeof(LivelineChart), new PropertyMetadata(10d, OnPlotPropertyChanged));

    /// <summary>The gap between grid lines. Default 10.</summary>
    public double AxisStep { get => (double)GetValue(AxisStepProperty); set => SetValue(AxisStepProperty, value); }

    public static readonly DependencyProperty AlertThresholdProperty = DependencyProperty.Register(
        nameof(AlertThreshold), typeof(double), typeof(LivelineChart), new PropertyMetadata(double.NaN, OnPlotPropertyChanged));

    /// <summary>A dashed threshold line; NaN hides it.</summary>
    public double AlertThreshold { get => (double)GetValue(AlertThresholdProperty); set => SetValue(AlertThresholdProperty, value); }

    public static readonly DependencyProperty AlertLabelProperty = DependencyProperty.Register(
        nameof(AlertLabel), typeof(string), typeof(LivelineChart), new PropertyMetadata(string.Empty, OnPlotPropertyChanged));

    /// <summary>The threshold's label, e.g. "Alert above 65". Hidden in the compact layout.</summary>
    public string AlertLabel { get => (string)GetValue(AlertLabelProperty); set => SetValue(AlertLabelProperty, value); }

    public static readonly DependencyProperty IsMotionReducedProperty = DependencyProperty.Register(
        nameof(IsMotionReduced), typeof(bool), typeof(LivelineChart), new PropertyMetadata(false, OnPlotPropertyChanged));

    /// <summary>Reduced motion: the line steps once per sample instead of gliding.</summary>
    public bool IsMotionReduced { get => (bool)GetValue(IsMotionReducedProperty); set => SetValue(IsMotionReducedProperty, value); }

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(LivelineChart), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>The line, its fill (at 7%), the live dot and the status dot. Default <c>PrimaryBrush</c>.</summary>
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public static readonly DependencyProperty MutedBrushProperty = DependencyProperty.Register(
        nameof(MutedBrush), typeof(Brush), typeof(LivelineChart), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>Axis labels, and the line when paused or disconnected. Default <c>OnSurfaceVariantBrush</c>.</summary>
    public Brush? MutedBrush { get => (Brush?)GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(LivelineChart), new PropertyMetadata(null, OnBrushChanged));

    /// <summary>Grid lines and the hatching of a gap. Default <c>LivelineGridBrush</c>.</summary>
    public Brush? GridBrush { get => (Brush?)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }

    public static readonly DependencyProperty ReconnectCommandProperty = DependencyProperty.Register(
        nameof(ReconnectCommand), typeof(ICommand), typeof(LivelineChart), new PropertyMetadata(null));

    /// <summary>Runs when Reconnect (shown while <see cref="LivelineState.Disconnected"/>) is pressed.</summary>
    public ICommand? ReconnectCommand { get => (ICommand?)GetValue(ReconnectCommandProperty); set => SetValue(ReconnectCommandProperty, value); }

    /// <summary>Raised when Reconnect is pressed.</summary>
    public event EventHandler? ReconnectRequested;

    /// <summary>Raised when the control changes <see cref="State"/> itself (pause and resume).</summary>
    public event EventHandler<LivelineState>? StateChanged;

    public static readonly DependencyProperty PausedTimeProperty = DependencyProperty.Register(
        nameof(PausedTime), typeof(DateTimeOffset?), typeof(LivelineChart), new PropertyMetadata(null));

    /// <summary>When the chart stopped (set by the control when a pause takes effect).</summary>
    public DateTimeOffset? PausedTime { get => (DateTimeOffset?)GetValue(PausedTimeProperty); private set => SetValue(PausedTimeProperty, value); }

    // Read-only texts the template shows.
    public static readonly DependencyProperty ValueTextProperty = Text(nameof(ValueText), "—");
    public string ValueText { get => (string)GetValue(ValueTextProperty); private set => SetValue(ValueTextProperty, value); }

    public static readonly DependencyProperty DeltaTextProperty = Text(nameof(DeltaText), string.Empty);
    public string DeltaText { get => (string)GetValue(DeltaTextProperty); private set => SetValue(DeltaTextProperty, value); }

    public static readonly DependencyProperty StatusTextProperty = Text(nameof(StatusText), "Connecting");
    public string StatusText { get => (string)GetValue(StatusTextProperty); private set => SetValue(StatusTextProperty, value); }

    public static readonly DependencyProperty MinTextProperty = Text(nameof(MinText), "—");
    public string MinText { get => (string)GetValue(MinTextProperty); private set => SetValue(MinTextProperty, value); }

    public static readonly DependencyProperty AvgTextProperty = Text(nameof(AvgText), "—");
    public string AvgText { get => (string)GetValue(AvgTextProperty); private set => SetValue(AvgTextProperty, value); }

    public static readonly DependencyProperty MaxTextProperty = Text(nameof(MaxText), "—");
    public string MaxText { get => (string)GetValue(MaxTextProperty); private set => SetValue(MaxTextProperty, value); }

    public static readonly DependencyProperty WindowTextProperty = Text(nameof(WindowText), "1 min");
    public string WindowText { get => (string)GetValue(WindowTextProperty); private set => SetValue(WindowTextProperty, value); }

    public static readonly DependencyProperty SamplesTextProperty = Text(nameof(SamplesText), "—");
    public string SamplesText { get => (string)GetValue(SamplesTextProperty); private set => SetValue(SamplesTextProperty, value); }

    private static DependencyProperty Text(string name, string fallback)
        => DependencyProperty.Register(name, typeof(string), typeof(LivelineChart), new PropertyMetadata(fallback));

    #endregion

    protected override void OnApplyTemplate()
    {
        if (_plot is not null)
        {
            _plot.KeyDown -= OnPlotKeyDown;
            _plot.PointerMoved -= OnPlotPointerMoved;
            _plot.PointerExited -= OnPlotPointerExited;
            _plot.LostFocus -= OnPlotLostFocus;
        }

        foreach (var b in new[] { _pause, _compactPause })
        {
            if (b is not null)
            {
                b.Click -= OnPauseClick;
            }
        }

        if (_reconnect is not null)
        {
            _reconnect.Click -= OnReconnectClick;
        }

        foreach (var w in _windowButtons)
        {
            w.Checked -= OnWindowButtonChecked;
        }

        _windowButtons.Clear();

        base.OnApplyTemplate();

        _canvas = GetTemplateChild(CanvasPart) as LivelineChartCanvas;
        _plot = GetTemplateChild(PlotPart) as Control;
        _pause = GetTemplateChild(PausePart) as ButtonBase;
        _compactPause = GetTemplateChild(CompactPausePart) as ButtonBase;
        _reconnect = GetTemplateChild(ReconnectPart) as ButtonBase;

        if (_plot is not null)
        {
            _plot.KeyDown += OnPlotKeyDown;
            _plot.PointerMoved += OnPlotPointerMoved;
            _plot.PointerExited += OnPlotPointerExited;
            _plot.LostFocus += OnPlotLostFocus;
        }

        foreach (var b in new[] { _pause, _compactPause })
        {
            if (b is not null)
            {
                b.Click += OnPauseClick;
            }
        }

        if (_reconnect is not null)
        {
            _reconnect.Click += OnReconnectClick;
        }

        // Window selectors: PART_Window1, PART_Window5, PART_Window15, and their compact twins PART_CompactWindow*.
        foreach (var prefix in new[] { "PART_Window", "PART_CompactWindow" })
        {
            foreach (var minutes in new[] { 1, 5, 15 })
            {
                if (GetTemplateChild(prefix + minutes) is ToggleButton t)
                {
                    t.Tag = minutes;
                    t.Checked += OnWindowButtonChecked;
                    _windowButtons.Add(t);
                }
            }
        }

        SyncWindowButtons();
        UpdateLayoutState(ActualWidth, useTransitions: false);
        PushColors();
        Refresh(useTransitions: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Pick the layout before the first measure, so a narrow card never lays out wide for a frame.
        if (!double.IsInfinity(availableSize.Width))
        {
            UpdateLayoutState(availableSize.Width, useTransitions: false);
        }

        return base.MeasureOverride(availableSize);
    }

    #region Property callbacks

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (LivelineChart)d;
        if (chart._observed is not null)
        {
            chart._observed.CollectionChanged -= chart.OnCollectionChanged;
        }

        chart._observed = e.NewValue as INotifyCollectionChanged;
        if (chart._observed is not null)
        {
            chart._observed.CollectionChanged += chart.OnCollectionChanged;
        }

        chart.OnSampleArrived();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSampleArrived();

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (LivelineChart)d;
        var state = (LivelineState)e.NewValue;
        if (state == LivelineState.Disconnected)
        {
            chart._disconnectedMs = chart._clock.Elapsed.TotalMilliseconds;
        }

        if (state == LivelineState.Paused)
        {
            // Hold what is on screen; new samples keep arriving in Data but are not drawn until Resume.
            chart._frozen = chart.Snapshot();
            chart.PausedTime ??= DateTimeOffset.Now;
        }
        else
        {
            chart.PausedTime = null;
        }

        chart._inspect = null;
        chart.Refresh(useTransitions: true);
    }

    private static void OnWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (LivelineChart)d;
        chart._inspect = null;
        chart.SyncWindowButtons();
        chart.Refresh(useTransitions: true);
    }

    private static void OnPlotPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LivelineChart)d).Refresh(useTransitions: true);

    private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LivelineChart)d).PushColors();

    #endregion

    #region Behaviour

    private void OnSampleArrived()
    {
        _arrivalMs = _clock.Elapsed.TotalMilliseconds;

        // A pause takes effect on the next sample: stop there and show when.
        if (State == LivelineState.Pausing)
        {
            PausedTime = DateTimeOffset.Now;
            SetStateFromControl(LivelineState.Paused);
            return;
        }

        if (State == LivelineState.Paused)
        {
            if (_frozen.Length == 0)
            {
                _frozen = Snapshot();
                Refresh(useTransitions: true);
            }

            return;
        }

        Refresh(useTransitions: true);
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        switch (State)
        {
            case LivelineState.Live:
                SetStateFromControl(LivelineState.Pausing);
                break;
            case LivelineState.Pausing:
            case LivelineState.Paused:
                _arrivalMs = _clock.Elapsed.TotalMilliseconds;
                SetStateFromControl(LivelineState.Live);
                break;
        }
    }

    private void SetStateFromControl(LivelineState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnReconnectClick(object sender, RoutedEventArgs e)
    {
        ReconnectRequested?.Invoke(this, EventArgs.Empty);
        if (ReconnectCommand?.CanExecute(null) == true)
        {
            ReconnectCommand.Execute(null);
        }
    }

    private void OnWindowButtonChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingWindow || sender is not ToggleButton { Tag: int minutes })
        {
            return;
        }

        Window = TimeSpan.FromMinutes(minutes);
    }

    private void SyncWindowButtons()
    {
        _syncingWindow = true;
        foreach (var b in _windowButtons)
        {
            b.IsChecked = b.Tag is int minutes && TimeSpan.FromMinutes(minutes) == Window;
        }

        _syncingWindow = false;
    }

    private void OnPlotKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var points = VisiblePoints(out _, withLead: true);
        if (points.Length == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.Right:
                var current = _inspect ?? points.Length - 1;
                _inspect = Math.Clamp(current + (e.Key == VirtualKey.Left ? -1 : 1), 0, points.Length - 1);
                break;
            case VirtualKey.Home:
                _inspect = 0;
                break;
            case VirtualKey.End:
                _inspect = points.Length - 1;
                break;
            case VirtualKey.Escape:
                _inspect = null;
                break;
            default:
                return;
        }

        e.Handled = true;
        PushToCanvas();
    }

    private void OnPlotPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_canvas is null || State == LivelineState.Connecting)
        {
            return;
        }

        var x = e.GetCurrentPoint(_canvas).Position.X;
        var index = _canvas.IndexAt(x);
        if (index != _inspect)
        {
            _inspect = index;
            PushToCanvas();
        }
    }

    private void OnPlotPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_inspect is not null && _plot?.FocusState is null or FocusState.Unfocused)
        {
            _inspect = null;
            PushToCanvas();
        }
    }

    private void OnPlotLostFocus(object sender, RoutedEventArgs e)
    {
        _inspect = null;
        PushToCanvas();
    }

    private void UpdateLayoutState(double width, bool useTransitions)
    {
        if (width <= 0 || double.IsNaN(width))
        {
            return;
        }

        var compact = width < CompactWidth;
        if (compact == _isCompact && _canvas is not null)
        {
            return;
        }

        _isCompact = compact;
        VisualStateManager.GoToState(this, compact ? "Compact" : "Regular", useTransitions);
        PushToCanvas();
    }

    private void UpdateTimer()
    {
        // Frames run only while something moves: the glide after a sample, or a growing gap while disconnected.
        var glide = State == LivelineState.Live && !IsMotionReduced && SinceArrival() < SampleIntervalMs();
        var gap = State == LivelineState.Disconnected;
        _frameTimer.Interval = TimeSpan.FromMilliseconds(gap ? 1000 : 33);
        if ((glide || gap) && IsLoaded)
        {
            _frameTimer.Start();
        }
        else
        {
            _frameTimer.Stop();
        }
    }

    private void OnFrame()
    {
        PushToCanvas();
        UpdateTimer();
    }

    #endregion

    #region State to texts and canvas

    private double SinceArrival() => _clock.Elapsed.TotalMilliseconds - _arrivalMs;

    private double SampleIntervalMs()
    {
        var data = Data;
        if (data is { Count: > 1 })
        {
            var ms = (data[^1].Time - data[^2].Time).TotalMilliseconds;
            if (ms > 0)
            {
                return Math.Min(ms, 5000);
            }
        }

        return 1000;
    }

    private LivelinePoint[] Snapshot() => Data is { } d ? [.. d] : [];

    private LivelinePoint[] Source() => State == LivelineState.Paused ? _frozen : Snapshot();

    /// <summary>The samples inside the window ending at the latest sample (plus the one before it, which the glide
    /// slides out past the left edge, when <paramref name="withLead"/>).</summary>
    private LivelinePoint[] VisiblePoints(out DateTimeOffset end, bool withLead = false)
    {
        var all = Source();
        if (all.Length == 0)
        {
            end = default;
            return all;
        }

        end = all[^1].Time;
        var start = end - Window - (withLead ? TimeSpan.FromMilliseconds(SampleIntervalMs() * 1.5) : TimeSpan.Zero);
        return [.. all.Where(p => p.Time >= start)];
    }

    private void Refresh(bool useTransitions)
    {
        var state = State;
        var points = VisiblePoints(out _);
        var hasData = points.Length > 0 && state != LivelineState.Connecting;

        ValueText = hasData ? Format(points[^1].Value) : "—";
        WindowText = WindowLabel(Window);
        if (hasData)
        {
            var delta = points[^1].Value - points[0].Value;
            DeltaText = (delta >= 0 ? "▲ +" : "▼ −") + Format(Math.Abs(delta)) + " vs " + Ago(Window.TotalSeconds);
            MinText = Format(points.Min(p => p.Value));
            AvgText = Format(points.Average(p => p.Value));
            MaxText = Format(points.Max(p => p.Value));
            SamplesText = points.Length.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            DeltaText = string.Empty;
            MinText = AvgText = MaxText = SamplesText = "—";
        }

        StatusText = state switch
        {
            LivelineState.Connecting => "Connecting",
            LivelineState.Live => "Live",
            LivelineState.Pausing => "Pausing…",
            LivelineState.Paused => "Paused · " + (PausedTime ?? DateTimeOffset.Now).ToLocalTime().ToString("h:mm:ss tt", CultureInfo.GetCultureInfo("en-US")),
            _ => "Disconnected",
        };

        VisualStateManager.GoToState(this, state.ToString(), useTransitions);

        var pauseName = state is LivelineState.Paused or LivelineState.Pausing ? "Resume stream" : "Pause stream";
        foreach (var b in new[] { _pause, _compactPause })
        {
            if (b is not null)
            {
                AutomationProperties.SetName(b, pauseName);
                b.IsEnabled = state is LivelineState.Live or LivelineState.Pausing or LivelineState.Paused;
            }
        }

        PushToCanvas();
        UpdateTimer();
    }

    private void PushToCanvas()
    {
        if (_canvas is null)
        {
            return;
        }

        var points = VisiblePoints(out var end, withLead: true);
        var state = State;
        var interval = SampleIntervalMs();

        // The glide: the plot's right edge trails the newest sample by one interval and catches up over it, so the
        // newest segment slides in from the right while the live dot rises or falls to its value.
        double progress = 1;
        if (state == LivelineState.Live && !IsMotionReduced)
        {
            progress = Math.Clamp(SinceArrival() / interval, 0, 1);
        }

        var renderEnd = end;
        if (state == LivelineState.Disconnected && points.Length > 0)
        {
            renderEnd = end + TimeSpan.FromMilliseconds(_clock.Elapsed.TotalMilliseconds - _disconnectedMs);
        }
        else if (points.Length > 1)
        {
            renderEnd = end - TimeSpan.FromMilliseconds(interval * (1 - progress));
        }

        if (_inspect is { } i && i >= points.Length)
        {
            _inspect = null;
        }

        _canvas.Update(new LivelineFrame(
            Points: points,
            WindowEnd: renderEnd,
            Window: Window,
            Glide: progress,
            State: state,
            AxisMinimum: AxisMinimum,
            AxisMaximum: AxisMaximum,
            AxisStep: AxisStep > 0 ? AxisStep : 10,
            AlertThreshold: AlertThreshold,
            AlertLabel: AlertLabel,
            Unit: Unit,
            Compact: _isCompact,
            Inspect: _inspect,
            WindowSeconds: Window.TotalSeconds));

        if (_plot is not null)
        {
            AutomationProperties.SetName(_plot, _inspect is { } k && k < points.Length
                ? $"{Format(points[k].Value)} {Unit}, {Ago((end - points[k].Time).TotalSeconds)}"
                : "Chart. Use left and right arrows to read points, Escape to close.");
        }
    }

    private void PushColors()
    {
        _canvas?.SetColors(
            accent: AccentBrush,
            ink: Foreground,
            muted: MutedBrush,
            grid: GridBrush,
            surface: Background);
        PushToCanvas();
    }

    internal static string Format(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>"now", "−45 s", "−1 min", "−3.75 min": axis labels count back from now.</summary>
    internal static string Ago(double seconds)
    {
        var s = Math.Round(seconds);
        if (s <= 0)
        {
            return "now";
        }

        return s >= 60
            ? "−" + (s / 60).ToString("0.##", CultureInfo.InvariantCulture) + " min"
            : "−" + s.ToString(CultureInfo.InvariantCulture) + " s";
    }

    private static string WindowLabel(TimeSpan window)
        => window.TotalMinutes >= 1
            ? window.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture) + " min"
            : window.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s";

    #endregion
}
