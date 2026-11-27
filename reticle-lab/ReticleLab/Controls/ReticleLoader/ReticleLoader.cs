using System.Windows.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;

namespace ReticleLab.Controls;

/// <summary>The phases of a <see cref="ReticleLoader"/>.</summary>
public enum ReticleState
{
    /// <summary>Size unknown: two bands counter-rotate and one arc sweeps.</summary>
    Scanning,
    /// <summary>Size known: the bands stop and the arc fills with <see cref="ReticleLoader.Value"/>.</summary>
    Locking,
    /// <summary>Done: the X closes in 480 ms, then nothing moves.</summary>
    Locked,
    /// <summary>Nothing queued: the HUD dims to 42%.</summary>
    Idle,
    /// <summary>Stopped part-way: keeps its progress and offers Resume.</summary>
    Interrupted,
}

/// <summary>
/// A HUD-style loader: a 300-unit reticle (two tick bands, rings, a progress arc, a closing X) with a percent,
/// a phase and a telemetry line, and a status line below. The look lives in Themes/ReticleLoader.xaml; this class
/// only maps <see cref="State"/>, <see cref="Value"/> and <see cref="IsMotionReduced"/> to visual states and draws
/// the progress arc, which depends on the value.
/// </summary>
[TemplatePart(Name = ProgressArcPart, Type = typeof(Path))]
[TemplatePart(Name = ProgressArcInkPart, Type = typeof(Path))]
[TemplatePart(Name = NodePart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = ResumeButtonPart, Type = typeof(ButtonBase))]
[TemplateVisualState(GroupName = "ReticleStates", Name = "Scanning")]
[TemplateVisualState(GroupName = "ReticleStates", Name = "ScanningReduced")]
[TemplateVisualState(GroupName = "ReticleStates", Name = "Locking")]
[TemplateVisualState(GroupName = "ReticleStates", Name = "Locked")]
[TemplateVisualState(GroupName = "ReticleStates", Name = "Idle")]
[TemplateVisualState(GroupName = "ReticleStates", Name = "Interrupted")]
[TemplateVisualState(GroupName = "StepStates", Name = "Steps0")]
[TemplateVisualState(GroupName = "StepStates", Name = "Steps1")]
[TemplateVisualState(GroupName = "StepStates", Name = "Steps2")]
[TemplateVisualState(GroupName = "StepStates", Name = "Steps3")]
public partial class ReticleLoader : Control
{
    private const string ProgressArcPart = "PART_ProgressArc";
    private const string ProgressArcInkPart = "PART_ProgressArcInk";
    private const string NodePart = "PART_Node";
    private const string ResumeButtonPart = "PART_ResumeButton";

    // Design units: a 300 x 300 canvas, the arc on r = 104 around (150, 150), starting at 12 o'clock.
    private const double Center = 150;
    private const double ArcRadius = 104;

    private Path? _arc;
    private Path? _arcInk;
    private FrameworkElement? _node;
    private ButtonBase? _resume;
    private bool _templateApplied;

    public ReticleLoader()
    {
        DefaultStyleKey = typeof(ReticleLoader);
    }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(ReticleState), typeof(ReticleLoader), new PropertyMetadata(ReticleState.Scanning, OnVisualPropertyChanged));

    /// <summary>The phase to show. Default <see cref="ReticleState.Scanning"/>.</summary>
    public ReticleState State
    {
        get => (ReticleState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ReticleLoader), new PropertyMetadata(0d, OnVisualPropertyChanged));

    /// <summary>Progress, 0 to 100. Drawn in <see cref="ReticleState.Locking"/> and kept in <see cref="ReticleState.Interrupted"/>.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty IsMotionReducedProperty = DependencyProperty.Register(
        nameof(IsMotionReduced), typeof(bool), typeof(ReticleLoader), new PropertyMetadata(false, OnVisualPropertyChanged));

    /// <summary>Reduced motion: Scanning is still (bands at rest, sweep parked at 12 o'clock) with a slow 2 s pulse on the sweep, and the status dots stop pulsing.</summary>
    public bool IsMotionReduced
    {
        get => (bool)GetValue(IsMotionReducedProperty);
        set => SetValue(IsMotionReducedProperty, value);
    }

    public static readonly DependencyProperty TelemetryProperty = DependencyProperty.Register(
        nameof(Telemetry), typeof(string), typeof(ReticleLoader), new PropertyMetadata(string.Empty));

    /// <summary>The small line on the lower-right leader, e.g. "212/331 · 14.2 MB/S". Authored uppercase.</summary>
    public string Telemetry
    {
        get => (string)GetValue(TelemetryProperty);
        set => SetValue(TelemetryProperty, value);
    }

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(ReticleLoader), new PropertyMetadata(string.Empty));

    /// <summary>The muted line under the status, e.g. "331 files · 6.8 s". Empty hides it.</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(ReticleLoader), new PropertyMetadata(null));

    /// <summary>The live marks: the sweep, the progress arc, its node, the lock dot and the percent. The default style uses <c>PrimaryBrush</c>.</summary>
    public Brush? AccentBrush
    {
        get => (Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public static readonly DependencyProperty StatusVisibilityProperty = DependencyProperty.Register(
        nameof(StatusVisibility), typeof(Visibility), typeof(ReticleLoader), new PropertyMetadata(Visibility.Visible));

    /// <summary>The status line, detail and Resume button under the ring. Collapse it when the host shows its own.</summary>
    public Visibility StatusVisibility
    {
        get => (Visibility)GetValue(StatusVisibilityProperty);
        set => SetValue(StatusVisibilityProperty, value);
    }

    public static readonly DependencyProperty ResumeCommandProperty = DependencyProperty.Register(
        nameof(ResumeCommand), typeof(ICommand), typeof(ReticleLoader), new PropertyMetadata(null));

    /// <summary>Runs when the Resume button (shown in <see cref="ReticleState.Interrupted"/>) is pressed.</summary>
    public ICommand? ResumeCommand
    {
        get => (ICommand?)GetValue(ResumeCommandProperty);
        set => SetValue(ResumeCommandProperty, value);
    }

    /// <summary>Raised when the Resume button is pressed.</summary>
    public event EventHandler? ResumeRequested;

    public static readonly DependencyProperty PercentTextProperty = DependencyProperty.Register(
        nameof(PercentText), typeof(string), typeof(ReticleLoader), new PropertyMetadata("—"));

    /// <summary>Read-only: "—" while the size is unknown, otherwise the rounded percent.</summary>
    public string PercentText
    {
        get => (string)GetValue(PercentTextProperty);
        private set => SetValue(PercentTextProperty, value);
    }

    public static readonly DependencyProperty PhaseTextProperty = DependencyProperty.Register(
        nameof(PhaseText), typeof(string), typeof(ReticleLoader), new PropertyMetadata("SCANNING"));

    /// <summary>Read-only: SCANNING, LOCKING, LOCKED, IDLE or PAUSED.</summary>
    public string PhaseText
    {
        get => (string)GetValue(PhaseTextProperty);
        private set => SetValue(PhaseTextProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ReticleLoader), new PropertyMetadata("Scanning"));

    /// <summary>Read-only: the status line, announced politely ("Locking · 64%", "Paused at 64%").</summary>
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        private set => SetValue(StatusTextProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        if (_resume is not null)
        {
            _resume.Click -= OnResumeClick;
        }

        base.OnApplyTemplate();
        _arc = GetTemplateChild(ProgressArcPart) as Path;
        _arcInk = GetTemplateChild(ProgressArcInkPart) as Path;
        _node = GetTemplateChild(NodePart) as FrameworkElement;
        _resume = GetTemplateChild(ResumeButtonPart) as ButtonBase;
        if (_resume is not null)
        {
            _resume.Click += OnResumeClick;
        }

        _templateApplied = true;
        UpdateAll(useTransitions: false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ReticleLoaderAutomationPeer(this);

    private void OnResumeClick(object sender, RoutedEventArgs e)
    {
        ResumeRequested?.Invoke(this, EventArgs.Empty);
        if (ResumeCommand?.CanExecute(null) == true)
        {
            ResumeCommand.Execute(null);
        }
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ReticleLoader)d).UpdateAll(useTransitions: true);

    private double Percent => Math.Clamp(double.IsNaN(Value) ? 0 : Value, 0, 100);

    private void UpdateAll(bool useTransitions)
    {
        var state = State;
        var p = (int)Math.Round(Percent);

        PercentText = state switch
        {
            ReticleState.Locking or ReticleState.Interrupted => p + "%",
            ReticleState.Locked => "100%",
            _ => "—",
        };
        PhaseText = state switch
        {
            ReticleState.Scanning => "SCANNING",
            ReticleState.Locking => "LOCKING",
            ReticleState.Locked => "LOCKED",
            ReticleState.Idle => "IDLE",
            _ => "PAUSED",
        };
        StatusText = state switch
        {
            ReticleState.Scanning => "Scanning",
            ReticleState.Locking => $"Locking · {p}%",
            ReticleState.Locked => "Locked",
            ReticleState.Idle => "Idle",
            _ => $"Paused at {p}%",
        };

        if (!_templateApplied)
        {
            return;
        }

        UpdateArc(state);

        var visual = state switch
        {
            ReticleState.Scanning => IsMotionReduced ? "ScanningReduced" : "Scanning",
            _ => state.ToString(),
        };
        VisualStateManager.GoToState(this, visual, useTransitions);

        // Status dots: one per phase step. Locking fills the second at half way.
        var steps = state switch
        {
            ReticleState.Locked => 3,
            ReticleState.Locking or ReticleState.Interrupted => p >= 50 ? 2 : 1,
            _ => 0,
        };
        VisualStateManager.GoToState(this, "Steps" + steps, useTransitions);

        if (FrameworkElementAutomationPeer.FromElement(this) is ReticleLoaderAutomationPeer peer)
        {
            peer.RaisePropertyChangedEvent(
                Microsoft.UI.Xaml.Automation.AutomationElementIdentifiers.ItemStatusProperty, string.Empty, peer.Status);
        }
    }

    private void UpdateArc(ReticleState state)
    {
        var p = state switch
        {
            ReticleState.Locked => 100,
            ReticleState.Locking or ReticleState.Interrupted => Percent,
            _ => 0,
        };

        // 3.599° per percent, so 100% stops a hair short of a closed circle (an arc cannot end where it starts).
        var angle = (-90 + 3.599 * p) * Math.PI / 180;
        var end = new Point(Center + ArcRadius * Math.Cos(angle), Center + ArcRadius * Math.Sin(angle));

        // Both arcs carry the same shape: the accent one shows while locking, the ink one keeps an interrupted run.
        if (_arc is not null)
        {
            _arc.Data = BuildArc(p, end);
        }

        if (_arcInk is not null)
        {
            _arcInk.Data = BuildArc(p, end);
        }

        if (_node?.RenderTransform is TranslateTransform move)
        {
            // The node rides the arc's head; in Scanning it waits at 12 o'clock.
            move.X = end.X - Center;
            move.Y = end.Y - (Center - ArcRadius);
        }
    }

    private static PathGeometry? BuildArc(double p, Point end)
    {
        if (p <= 0)
        {
            return null;
        }

        var figure = new PathFigure { StartPoint = new Point(Center, Center - ArcRadius), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(ArcRadius, ArcRadius),
            IsLargeArc = p > 50,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    internal string AccessibleStatus => State switch
    {
        ReticleState.Scanning => "Scanning, size unknown",
        ReticleState.Locking => $"Locking, {(int)Math.Round(Percent)} percent",
        ReticleState.Locked => "Complete",
        ReticleState.Idle => "Idle",
        _ => $"Paused at {(int)Math.Round(Percent)} percent",
    };
}

/// <summary>Reports the loader as a progress bar whose item status reads like the prototype's aria-valuetext.</summary>
public sealed partial class ReticleLoaderAutomationPeer : FrameworkElementAutomationPeer
{
    public ReticleLoaderAutomationPeer(ReticleLoader owner) : base(owner)
    {
    }

    internal string Status => ((ReticleLoader)Owner).AccessibleStatus;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override string GetClassNameCore() => nameof(ReticleLoader);

    protected override string GetItemStatusCore() => Status;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? "Loader" : name;
    }
}
