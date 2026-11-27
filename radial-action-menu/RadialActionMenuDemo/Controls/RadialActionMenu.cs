using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.ViewManagement;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace RadialActionMenuDemo.Controls;

/// <summary>
/// A floating action button that opens up to five actions on a 90° arc, 22.5° apart. Keys 1–5 pick, the arrows move,
/// Esc closes; a finger can slide from the trigger to an item, whose name shows in a readout, and release to pick.
/// The control fills its container and places the trigger <see cref="EdgeMargin"/> from the corner set by
/// <see cref="Position"/>; put it in the same Grid cell as the content it covers.
/// </summary>
[TemplatePart(Name = CanvasPart, Type = typeof(Canvas))]
[TemplatePart(Name = TriggerPart, Type = typeof(Button))]
[TemplatePart(Name = ScrimPart, Type = typeof(UIElement))]
[TemplatePart(Name = GuidePart, Type = typeof(Path))]
[TemplatePart(Name = HintPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = ReadoutPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PlusRotationPart, Type = typeof(RotateTransform))]
[TemplateVisualState(GroupName = "OpenStates", Name = "Closed")]
[TemplateVisualState(GroupName = "OpenStates", Name = "Open")]
[TemplateVisualState(GroupName = "HintStates", Name = "HintHidden")]
[TemplateVisualState(GroupName = "HintStates", Name = "HintVisible")]
[TemplateVisualState(GroupName = "SlideStates", Name = "NotSliding")]
[TemplateVisualState(GroupName = "SlideStates", Name = "Sliding")]
public partial class RadialActionMenu : Control
{
    private const string CanvasPart = "PART_Canvas";
    private const string TriggerPart = "PART_Trigger";
    private const string ScrimPart = "PART_Scrim";
    private const string GuidePart = "PART_Guide";
    private const string HintPart = "PART_Hint";
    private const string ReadoutPart = "PART_Readout";
    private const string PlusRotationPart = "PART_PlusRotation";

    public const int MaxItems = 5;
    private const double StepDegrees = 22.5;

    // Motion from the design: 30 ms stagger out with a 320 ms settle, reverse on close (20 ms stagger, 200 ms).
    private static readonly TimeSpan OpenStagger = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan OpenMove = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan OpenFade = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan CloseStagger = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan CloseMove = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan CloseFade = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan ScrimFade = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan GuideFade = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan PlusTurn = TimeSpan.FromMilliseconds(240);

    private Canvas? _canvas;
    private Button? _trigger;
    private UIElement? _scrim;
    private Path? _guide;
    private FrameworkElement? _hint;
    private FrameworkElement? _readout;
    private RotateTransform? _plusRotation;
    private readonly List<RadialActionMenuItem> _items = new();
    private Storyboard? _running;
    private Point _anchor;
    private bool _isReady;

    private bool _openedByPress;
    private bool _sliding;
    private DateTime _lastPress;
    private bool _triggerHover;
    private bool _triggerKeyboardFocus;

    public event EventHandler? Opening;
    public event EventHandler? Opened;
    public event EventHandler? Closing;
    public event EventHandler? Closed;
    public event EventHandler<RadialMenuItemData>? ItemSelected;

    public RadialActionMenu()
    {
        DefaultStyleKey = typeof(RadialActionMenu);
        SizeChanged += (_, _) => LayoutItems();
        // Preview (tunnelling) so the menu sees keys before an item or directional focus navigation acts on them.
        PreviewKeyDown += OnKeyDown;
    }

    #region Dependency properties

    public static DependencyProperty ItemsProperty { get; } = DependencyProperty.Register(
        nameof(Items), typeof(ObservableCollection<RadialMenuItemData>), typeof(RadialActionMenu),
        new PropertyMetadata(null, (s, e) => ((RadialActionMenu)s).OnItemsChanged(
            e.OldValue as ObservableCollection<RadialMenuItemData>, e.NewValue as ObservableCollection<RadialMenuItemData>)));

    /// <summary>The actions, up to five. Changes to the collection update the menu.</summary>
    public ObservableCollection<RadialMenuItemData>? Items
    {
        get => (ObservableCollection<RadialMenuItemData>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static DependencyProperty PositionProperty { get; } = DependencyProperty.Register(
        nameof(Position), typeof(MenuPosition), typeof(RadialActionMenu),
        new PropertyMetadata(MenuPosition.BottomRight, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    /// <summary>The corner the trigger rests in. The arc opens away from the edges (it mirrors on the left).</summary>
    public MenuPosition Position
    {
        get => (MenuPosition)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public static DependencyProperty IsOpenProperty { get; } = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(RadialActionMenu),
        new PropertyMetadata(false, (s, e) => ((RadialActionMenu)s).OnIsOpenChanged((bool)e.NewValue)));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static DependencyProperty RadiusProperty { get; } = DependencyProperty.Register(
        nameof(Radius), typeof(double), typeof(RadialActionMenu),
        new PropertyMetadata(208d, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    /// <summary>Distance from the trigger's centre to the discs' centres: 208 px desktop, 172 px phone.</summary>
    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public static DependencyProperty DiscSizeProperty { get; } = DependencyProperty.Register(
        nameof(DiscSize), typeof(double), typeof(RadialActionMenu),
        new PropertyMetadata(52d, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    /// <summary>Disc diameter (touch target): 52 px desktop, 56 px phone.</summary>
    public double DiscSize
    {
        get => (double)GetValue(DiscSizeProperty);
        set => SetValue(DiscSizeProperty, value);
    }

    public static DependencyProperty TriggerSizeProperty { get; } = DependencyProperty.Register(
        nameof(TriggerSize), typeof(double), typeof(RadialActionMenu),
        new PropertyMetadata(64d, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    public double TriggerSize
    {
        get => (double)GetValue(TriggerSizeProperty);
        set => SetValue(TriggerSizeProperty, value);
    }

    public static DependencyProperty EdgeMarginProperty { get; } = DependencyProperty.Register(
        nameof(EdgeMargin), typeof(double), typeof(RadialActionMenu),
        new PropertyMetadata(24d, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    /// <summary>The trigger rests this far from both edges (24 px).</summary>
    public double EdgeMargin
    {
        get => (double)GetValue(EdgeMarginProperty);
        set => SetValue(EdgeMarginProperty, value);
    }

    public static DependencyProperty ShowLabelsProperty { get; } = DependencyProperty.Register(
        nameof(ShowLabels), typeof(bool), typeof(RadialActionMenu),
        new PropertyMetadata(true, (s, e) => ((RadialActionMenu)s).LayoutItems()));

    /// <summary>
    /// True: each disc has a label pill with its key. False (touch layout): only the key badge, and the name shows in
    /// the readout while a finger slides over an item.
    /// </summary>
    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public static DependencyProperty TriggerLabelProperty { get; } = DependencyProperty.Register(
        nameof(TriggerLabel), typeof(string), typeof(RadialActionMenu),
        new PropertyMetadata("Quick actions", (s, e) => ((RadialActionMenu)s).UpdateTriggerName()));

    /// <summary>The trigger's name, shown in the hint ("Quick actions · Space") and read by screen readers.</summary>
    public string TriggerLabel
    {
        get => (string)GetValue(TriggerLabelProperty);
        set => SetValue(TriggerLabelProperty, value);
    }

    public static DependencyProperty IsHintVisibleProperty { get; } = DependencyProperty.Register(
        nameof(IsHintVisible), typeof(bool), typeof(RadialActionMenu),
        new PropertyMetadata(false, (s, e) => ((RadialActionMenu)s).UpdateStates(true)));

    /// <summary>The "Quick actions · Space" hint beside the closed trigger. Set on hover or keyboard focus; settable.</summary>
    public bool IsHintVisible
    {
        get => (bool)GetValue(IsHintVisibleProperty);
        set => SetValue(IsHintVisibleProperty, value);
    }

    public static DependencyProperty HighlightedIndexProperty { get; } = DependencyProperty.Register(
        nameof(HighlightedIndex), typeof(int), typeof(RadialActionMenu),
        new PropertyMetadata(-1, (s, e) => ((RadialActionMenu)s).OnHighlightChanged()));

    /// <summary>
    /// The item under a finger sliding from the trigger (-1 for none). The disc fills with ink and the readout shows
    /// "key Name · release to open". Settable, to show the slide state.
    /// </summary>
    public int HighlightedIndex
    {
        get => (int)GetValue(HighlightedIndexProperty);
        set => SetValue(HighlightedIndexProperty, value);
    }

    public static DependencyProperty HighlightedLabelProperty { get; } = DependencyProperty.Register(
        nameof(HighlightedLabel), typeof(string), typeof(RadialActionMenu), new PropertyMetadata(string.Empty));

    /// <summary>The highlighted item's name (read by the readout).</summary>
    public string HighlightedLabel
    {
        get => (string)GetValue(HighlightedLabelProperty);
        private set => SetValue(HighlightedLabelProperty, value);
    }

    public static DependencyProperty HighlightedKeyProperty { get; } = DependencyProperty.Register(
        nameof(HighlightedKey), typeof(string), typeof(RadialActionMenu), new PropertyMetadata(string.Empty));

    public string HighlightedKey
    {
        get => (string)GetValue(HighlightedKeyProperty);
        private set => SetValue(HighlightedKeyProperty, value);
    }

    #endregion

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
    public void Toggle() => IsOpen = !IsOpen;

    protected override void OnApplyTemplate()
    {
        if (_trigger != null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.RemoveHandler(PointerPressedEvent, (PointerEventHandler)OnTriggerPointerPressed);
            _trigger.RemoveHandler(PointerMovedEvent, (PointerEventHandler)OnTriggerPointerMoved);
            _trigger.RemoveHandler(PointerReleasedEvent, (PointerEventHandler)OnTriggerPointerReleased);
            _trigger.PointerEntered -= OnTriggerPointerEntered;
            _trigger.PointerExited -= OnTriggerPointerExited;
            _trigger.GotFocus -= OnTriggerGotFocus;
            _trigger.LostFocus -= OnTriggerLostFocus;
        }
        if (_scrim != null) _scrim.Tapped -= OnScrimTapped;

        base.OnApplyTemplate();

        _canvas = GetTemplateChild(CanvasPart) as Canvas;
        _trigger = GetTemplateChild(TriggerPart) as Button;
        _scrim = GetTemplateChild(ScrimPart) as UIElement;
        _guide = GetTemplateChild(GuidePart) as Path;
        _hint = GetTemplateChild(HintPart) as FrameworkElement;
        _readout = GetTemplateChild(ReadoutPart) as FrameworkElement;
        _plusRotation = GetTemplateChild(PlusRotationPart) as RotateTransform;

        if (_trigger != null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.AddHandler(PointerPressedEvent, (PointerEventHandler)OnTriggerPointerPressed, true);
            _trigger.AddHandler(PointerMovedEvent, (PointerEventHandler)OnTriggerPointerMoved, true);
            _trigger.AddHandler(PointerReleasedEvent, (PointerEventHandler)OnTriggerPointerReleased, true);
            _trigger.PointerEntered += OnTriggerPointerEntered;
            _trigger.PointerExited += OnTriggerPointerExited;
            _trigger.GotFocus += OnTriggerGotFocus;
            _trigger.LostFocus += OnTriggerLostFocus;
        }
        if (_scrim != null) _scrim.Tapped += OnScrimTapped;
        if (_hint != null) _hint.SizeChanged += (_, _) => PositionChrome();
        if (_readout != null) _readout.SizeChanged += (_, _) => PositionChrome();

        _isReady = true;
        RebuildItems();
        UpdateTriggerName();
        ApplyOpenState(IsOpen, animate: false);
    }

    #region Items and layout

    private void OnItemsChanged(ObservableCollection<RadialMenuItemData>? oldItems, ObservableCollection<RadialMenuItemData>? newItems)
    {
        if (oldItems != null) oldItems.CollectionChanged -= OnCollectionChanged;
        if (newItems != null) newItems.CollectionChanged += OnCollectionChanged;
        RebuildItems();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void RebuildItems()
    {
        if (!_isReady || _canvas == null) return;

        foreach (var item in _items)
        {
            item.Click -= OnItemClick;
            _canvas.Children.Remove(item);
        }
        _items.Clear();

        var data = Items?.Take(MaxItems).ToList() ?? new List<RadialMenuItemData>();
        // Items go in right after the guide, below the trigger, readout and hint.
        var insertAt = _guide != null ? _canvas.Children.IndexOf(_guide) + 1 : 0;
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var item = new RadialActionMenuItem
            {
                Data = d,
                Label = d.Label,
                KeyLabel = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                IconData = RadialActionMenuItem.ParseIcon(d.Icon),
                IsEnabled = d.IsEnabled,
                Command = d.Command,
                CommandParameter = d.CommandParameter,
                IsTabStop = IsOpen,
            };
            AutomationProperties.SetName(item, $"{d.Label}, key {i + 1}");
            item.Click += OnItemClick;
            item.DiscCenterChanged += (_, _) => PlaceItem(item);
            _items.Add(item);
            _canvas.Children.Insert(insertAt + i, item);
        }

        LayoutItems();
        ApplyOpenState(IsOpen, animate: false);
        OnHighlightChanged();
    }

    private bool IsLeft => Position is MenuPosition.BottomLeft or MenuPosition.TopLeft;
    private bool IsTop => Position is MenuPosition.TopLeft or MenuPosition.TopRight;

    private double StartAngle => Position switch
    {
        MenuPosition.BottomLeft or MenuPosition.TopLeft => 0,
        MenuPosition.Center => 135,
        _ => 180,
    };

    // Degrees per step in maths orientation (y up): bottom-right sweeps 180→90, bottom-left 0→90, top corners downward.
    private double Step => Position switch
    {
        MenuPosition.BottomLeft => StepDegrees,
        MenuPosition.TopLeft => -StepDegrees,
        MenuPosition.TopRight => StepDegrees,
        _ => -StepDegrees,
    };

    private Point ItemCentre(int i)
    {
        var a = (StartAngle + Step * i) * Math.PI / 180;
        return new Point(_anchor.X + Radius * Math.Cos(a), _anchor.Y - Radius * Math.Sin(a));
    }

    private void LayoutItems()
    {
        if (!_isReady || _canvas == null || _trigger == null) return;

        double w = ActualWidth, h = ActualHeight, t = TriggerSize, m = EdgeMargin;
        var cx = Position switch
        {
            MenuPosition.BottomLeft or MenuPosition.TopLeft => m + t / 2,
            MenuPosition.Center => w / 2,
            _ => w - m - t / 2,
        };
        var cy = IsTop ? m + t / 2 : h - m - t / 2;
        _anchor = new Point(cx, cy);

        _trigger.Width = _trigger.Height = t;
        Canvas.SetLeft(_trigger, cx - t / 2);
        Canvas.SetTop(_trigger, cy - t / 2);

        var d = DiscSize;
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var c = ItemCentre(i);
            item.DiscSize = d;
            item.IconSize = d >= 52 ? 22 : 18;
            item.Placement = !ShowLabels ? LabelPlacement.BadgeOnly : PlacementFor(StartAngle + Step * i);
            PlaceItem(item);
        }

        if (_guide != null)
        {
            _guide.Data = _items.Count > 1 ? BuildGuide() : null;
        }
        PositionChrome();
    }

    // The item includes its label pill, so it is placed by its disc centre, which it reports after layout.
    private void PlaceItem(RadialActionMenuItem item)
    {
        var i = _items.IndexOf(item);
        if (i < 0) return;
        var c = ItemCentre(i);
        Canvas.SetLeft(item, c.X - item.DiscCenter.X);
        Canvas.SetTop(item, c.Y - item.DiscCenter.Y);
    }

    private LabelPlacement PlacementFor(double angle)
    {
        var a = ((angle % 360) + 360) % 360;
        if (Position == MenuPosition.Center) return LabelPlacement.Above;
        if (Math.Abs(a - 90) < 0.1) return LabelPlacement.Above;
        if (Math.Abs(a - 270) < 0.1) return LabelPlacement.Below;
        return IsLeft ? LabelPlacement.Right : LabelPlacement.Left;
    }

    // The dotted guide: an arc through the disc centres (1.5 px, 1 px dots every 8 px).
    private Geometry BuildGuide()
    {
        var first = ItemCentre(0);
        var last = ItemCentre(_items.Count - 1);
        var figure = new PathFigure { StartPoint = first, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = last,
            Size = new Size(Radius, Radius),
            SweepDirection = Step < 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void PositionChrome()
    {
        double t = TriggerSize;
        if (_hint != null)
        {
            var x = IsLeft ? _anchor.X + t / 2 + 12 : _anchor.X - t / 2 - 12 - _hint.ActualWidth;
            Canvas.SetLeft(_hint, x);
            Canvas.SetTop(_hint, _anchor.Y - _hint.ActualHeight / 2);
        }
        if (_readout != null)
        {
            // Above the arc on the side away from the trigger, where the finger does not cover it.
            var top = IsTop ? _anchor.Y + Radius + DiscSize / 2 + 24 : _anchor.Y - Radius - DiscSize / 2 - 24 - _readout.ActualHeight;
            var left = IsLeft ? Math.Max(EdgeMargin, ActualWidth - EdgeMargin - _readout.ActualWidth) : EdgeMargin;
            if (Position == MenuPosition.Center) left = (ActualWidth - _readout.ActualWidth) / 2;
            Canvas.SetLeft(_readout, left);
            Canvas.SetTop(_readout, Math.Max(8, top));
        }
    }

    #endregion

    #region Open and close

    private void OnIsOpenChanged(bool open)
    {
        if (open) Opening?.Invoke(this, EventArgs.Empty);
        else Closing?.Invoke(this, EventArgs.Empty);
        ApplyOpenState(open, animate: true);
        if (!open) HighlightedIndex = -1;
        UpdateTriggerName();
    }

    private void ApplyOpenState(bool open, bool animate)
    {
        if (!_isReady) return;

        _running?.Stop();
        _running = null;

        foreach (var item in _items)
        {
            item.IsTabStop = open;
            item.IsHitTestVisible = open;
        }
        if (_scrim != null) _scrim.IsHitTestVisible = open;
        if (open) IsHintVisible = false;
        UpdateStates(animate);

        var reduced = !new UISettings().AnimationsEnabled;
        if (!animate)
        {
            SetEndState(open);
            return;
        }

        var sb = new Storyboard();
        var moveSpline = open ? Spline(0.2, 0.9, 0.3, 1) : Spline(0.4, 0, 1, 1);
        var ease = Spline(0.25, 0.1, 0.25, 1);
        var n = _items.Count;
        for (var i = 0; i < n; i++)
        {
            var item = _items[i];
            var transform = EnsureTransform(item);
            var c = ItemCentre(i);
            double dx = _anchor.X - c.X, dy = _anchor.Y - c.Y;
            var delay = open ? OpenStagger * i : CloseStagger * (n - 1 - i);
            var move = open ? OpenMove : CloseMove;
            var fade = open ? OpenFade : CloseFade;

            if (reduced)
            {
                // Reduced motion: no travel or scale, a 120 ms fade only.
                transform.TranslateX = transform.TranslateY = 0;
                transform.ScaleX = transform.ScaleY = 1;
                sb.Children.Add(Animate(item, "Opacity", open ? 0 : 1, open ? 1 : 0, TimeSpan.FromMilliseconds(120), TimeSpan.Zero, null));
                continue;
            }

            sb.Children.Add(Animate(transform, nameof(CompositeTransform.TranslateX), open ? dx : 0, open ? 0 : dx, move, delay, moveSpline));
            sb.Children.Add(Animate(transform, nameof(CompositeTransform.TranslateY), open ? dy : 0, open ? 0 : dy, move, delay, moveSpline));
            sb.Children.Add(Animate(transform, nameof(CompositeTransform.ScaleX), open ? 0.4 : 1, open ? 1 : 0.4, move, delay, moveSpline));
            sb.Children.Add(Animate(transform, nameof(CompositeTransform.ScaleY), open ? 0.4 : 1, open ? 1 : 0.4, move, delay, moveSpline));
            sb.Children.Add(Animate(item, "Opacity", open ? 0 : 1, open ? 1 : 0, fade, delay, ease));
        }

        if (_scrim != null) sb.Children.Add(Animate(_scrim, "Opacity", open ? 0 : 1, open ? 1 : 0, ScrimFade, TimeSpan.Zero, ease));
        if (_guide != null) sb.Children.Add(Animate(_guide, "Opacity", open ? 0 : 1, open ? 1 : 0, GuideFade, TimeSpan.Zero, ease));
        if (_plusRotation != null && !reduced)
            sb.Children.Add(Animate(_plusRotation, nameof(RotateTransform.Angle), open ? 0 : 45, open ? 45 : 0, PlusTurn, TimeSpan.Zero, Spline(0.2, 0.9, 0.3, 1)));
        else if (_plusRotation != null)
            _plusRotation.Angle = open ? 45 : 0;

        sb.Completed += (_, _) =>
        {
            if (_running != sb) return;
            _running = null;
            // Release the held animated values; local end values take over (a holding storyboard kept the items
            // on a cached layer, so later state changes inside them did not repaint).
            sb.Stop();
            SetEndState(open);
            if (open) Opened?.Invoke(this, EventArgs.Empty);
            else Closed?.Invoke(this, EventArgs.Empty);
        };
        _running = sb;
        sb.Begin();
    }

    private void SetEndState(bool open)
    {
        foreach (var item in _items)
        {
            item.Opacity = open ? 1 : 0;
            if (open)
            {
                // At rest the items carry no transform: Skia then repaints focus and hover changes on the exact bounds.
                item.RenderTransform = null;
            }
            else
            {
                var transform = EnsureTransform(item);
                transform.TranslateX = transform.TranslateY = 0;
                transform.ScaleX = transform.ScaleY = 0.4;
            }
        }
        if (_scrim != null) _scrim.Opacity = open ? 1 : 0;
        if (_guide != null) _guide.Opacity = open ? 1 : 0;
        if (_plusRotation != null) _plusRotation.Angle = open ? 45 : 0;
    }

    private static CompositeTransform EnsureTransform(UIElement element)
    {
        if (element.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            element.RenderTransform = transform;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        return transform;
    }

    private static KeySpline Spline(double x1, double y1, double x2, double y2)
        => new() { ControlPoint1 = new Point(x1, y1), ControlPoint2 = new Point(x2, y2) };

    private static Timeline Animate(DependencyObject target, string property, double from, double to, TimeSpan duration, TimeSpan delay, KeySpline? spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { BeginTime = delay };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = from });
        animation.KeyFrames.Add(spline != null
            ? new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(duration), Value = to, KeySpline = spline }
            : new LinearDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(duration), Value = to });
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void UpdateStates(bool useTransitions)
    {
        if (!_isReady) return;
        VisualStateManager.GoToState(this, IsOpen ? "Open" : "Closed", useTransitions);
        VisualStateManager.GoToState(this, IsHintVisible && !IsOpen ? "HintVisible" : "HintHidden", useTransitions);
        VisualStateManager.GoToState(this, HighlightedIndex >= 0 && IsOpen ? "Sliding" : "NotSliding", useTransitions);
    }

    private void UpdateTriggerName()
    {
        if (_trigger != null)
        {
            AutomationProperties.SetName(_trigger, IsOpen ? "Close " + TriggerLabel.ToLowerInvariant() : TriggerLabel);
        }
    }

    #endregion

    #region Picking

    private void Pick(RadialActionMenuItem item)
    {
        if (item.Data is not { IsEnabled: true } data) return;
        if (data.Command?.CanExecute(data.CommandParameter) == true) data.Command.Execute(data.CommandParameter);
        ItemSelected?.Invoke(this, data);
        CloseAndRefocus();
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadialActionMenuItem item)
        {
            // The item's own Command already ran through Button; ItemSelected and closing remain.
            if (item.Data is { IsEnabled: true } data)
            {
                ItemSelected?.Invoke(this, data);
                CloseAndRefocus();
            }
        }
    }

    private void CloseAndRefocus()
    {
        IsOpen = false;
        _trigger?.Focus(FocusState.Programmatic);
    }

    private void OnScrimTapped(object sender, TappedRoutedEventArgs e)
    {
        if (IsOpen) IsOpen = false;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsOpen) return;

        if (e.Key == VirtualKey.Escape)
        {
            CloseAndRefocus();
            e.Handled = true;
            return;
        }

        var number = e.Key switch
        {
            >= VirtualKey.Number1 and <= VirtualKey.Number5 => e.Key - VirtualKey.Number1,
            >= VirtualKey.NumberPad1 and <= VirtualKey.NumberPad5 => e.Key - VirtualKey.NumberPad1,
            _ => -1,
        };
        if (number >= 0)
        {
            if (number < _items.Count) Pick(_items[number]);
            e.Handled = true;
            return;
        }

        var direction = e.Key switch
        {
            VirtualKey.Up or VirtualKey.Right => 1,
            VirtualKey.Down or VirtualKey.Left => -1,
            _ => 0,
        };
        if (direction != 0 && _items.Count > 0)
        {
            var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) : null;
            var current = _items.FindIndex(i => ReferenceEquals(i, focused));
            for (var step = 1; step <= _items.Count; step++)
            {
                var next = ((current < 0 ? (direction > 0 ? -1 : 0) : current) + direction * step + _items.Count * 2) % _items.Count;
                if (_items[next].IsEnabled)
                {
                    _items[next].Focus(FocusState.Keyboard);
                    break;
                }
            }
            e.Handled = true;
        }
    }

    #endregion

    #region Trigger: click, hint, slide

    private void OnTriggerClick(object sender, RoutedEventArgs e)
    {
        if (_openedByPress)
        {
            _openedByPress = false;
            return;
        }

        var byKeyboard = (DateTime.UtcNow - _lastPress).TotalMilliseconds > 600;
        IsOpen = !IsOpen;
        if (IsOpen && byKeyboard)
        {
            // Opened from the keyboard: focus goes to the first available item.
            _items.FirstOrDefault(i => i.IsEnabled)?.Focus(FocusState.Keyboard);
        }
    }

    private void OnTriggerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _lastPress = DateTime.UtcNow;
        if (!IsOpen)
        {
            // Press opens at once, so a finger can slide straight to an item and release on it.
            _openedByPress = true;
            IsOpen = true;
        }
        _sliding = true;
    }

    private void OnTriggerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_sliding || _canvas == null || !IsOpen) return;
        var p = e.GetCurrentPoint(_canvas).Position;
        var reach = DiscSize / 2 + 12;
        var hit = -1;
        for (var i = 0; i < _items.Count; i++)
        {
            if (!_items[i].IsEnabled) continue;
            var c = ItemCentre(i);
            if (Math.Abs(c.X - p.X) <= reach && Math.Abs(c.Y - p.Y) <= reach)
            {
                hit = i;
                break;
            }
        }
        HighlightedIndex = hit;
    }

    private void OnTriggerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_sliding) return;
        _sliding = false;
        var index = HighlightedIndex;
        HighlightedIndex = -1;
        if (index >= 0 && index < _items.Count)
        {
            _openedByPress = false;
            Pick(_items[index]);
        }
    }

    private void OnHighlightChanged()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].IsHighlighted = i == HighlightedIndex;
        }
        var item = HighlightedIndex >= 0 && HighlightedIndex < _items.Count ? _items[HighlightedIndex] : null;
        HighlightedLabel = item?.Label ?? string.Empty;
        HighlightedKey = item?.KeyLabel ?? string.Empty;
        UpdateStates(true);
    }

    private void OnTriggerPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _triggerHover = true;
        IsHintVisible = !IsOpen;
    }

    private void OnTriggerPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _triggerHover = false;
        IsHintVisible = _triggerKeyboardFocus && !IsOpen;
    }

    private void OnTriggerGotFocus(object sender, RoutedEventArgs e)
    {
        _triggerKeyboardFocus = _trigger?.FocusState == FocusState.Keyboard;
        IsHintVisible = (_triggerKeyboardFocus || _triggerHover) && !IsOpen;
    }

    private void OnTriggerLostFocus(object sender, RoutedEventArgs e)
    {
        _triggerKeyboardFocus = false;
        IsHintVisible = _triggerHover && !IsOpen;
    }

    #endregion
}
