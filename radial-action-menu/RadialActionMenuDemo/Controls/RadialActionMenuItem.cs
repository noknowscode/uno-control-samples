using Microsoft.UI.Xaml.Markup;
using Windows.Foundation;

namespace RadialActionMenuDemo.Controls;

/// <summary>
/// One disc on the radial menu: an outlined icon disc, a label pill with its key badge (or the badge alone on the
/// disc in the touch layout), hover, keyboard-focus and touch-highlight states. Created by <see cref="RadialActionMenu"/>.
/// </summary>
[TemplatePart(Name = DiscPart, Type = typeof(FrameworkElement))]
[TemplateVisualState(GroupName = "LabelStates", Name = "LabelLeft")]
[TemplateVisualState(GroupName = "LabelStates", Name = "LabelRight")]
[TemplateVisualState(GroupName = "LabelStates", Name = "LabelAbove")]
[TemplateVisualState(GroupName = "LabelStates", Name = "LabelBelow")]
[TemplateVisualState(GroupName = "LabelStates", Name = "BadgeOnly")]
[TemplateVisualState(GroupName = "HighlightStates", Name = "NotHighlighted")]
[TemplateVisualState(GroupName = "HighlightStates", Name = "Highlighted")]
[TemplateVisualState(GroupName = "KeyboardFocusStates", Name = "NotKeyboardFocused")]
[TemplateVisualState(GroupName = "KeyboardFocusStates", Name = "KeyboardFocused")]
public partial class RadialActionMenuItem : Button
{
    private const string DiscPart = "PART_Disc";

    private FrameworkElement? _disc;

    public RadialActionMenuItem()
    {
        DefaultStyleKey = typeof(RadialActionMenuItem);
        SizeChanged += (_, _) => UpdateDiscCenter();
        // Drive the focus look explicitly: arrow-key moves between items did not always refresh Button's own
        // FocusStates on Skia, leaving two items looking focused.
        GotFocus += (_, _) => UpdateFocus();
        LostFocus += (_, _) => UpdateFocus();
    }

    private void UpdateFocus()
        => VisualStateManager.GoToState(this, FocusState == FocusState.Keyboard ? "KeyboardFocused" : "NotKeyboardFocused", false);

    /// <summary>The disc's centre in the item's coordinates (the label pill is part of the item).</summary>
    public Point DiscCenter { get; private set; }

    /// <summary>Raised when <see cref="DiscCenter"/> moves (label placement or size changed).</summary>
    public event EventHandler? DiscCenterChanged;

    private void UpdateDiscCenter()
    {
        if (_disc == null) return;
        var p = _disc.TransformToVisual(this).TransformPoint(new Point(_disc.ActualWidth / 2, _disc.ActualHeight / 2));
        if (p != DiscCenter)
        {
            DiscCenter = p;
            DiscCenterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static DependencyProperty LabelProperty { get; } = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(RadialActionMenuItem), new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static DependencyProperty KeyLabelProperty { get; } = DependencyProperty.Register(
        nameof(KeyLabel), typeof(string), typeof(RadialActionMenuItem), new PropertyMetadata(string.Empty));

    /// <summary>The shortcut shown in the badge ("1" to "5").</summary>
    public string KeyLabel
    {
        get => (string)GetValue(KeyLabelProperty);
        set => SetValue(KeyLabelProperty, value);
    }

    public static DependencyProperty IconDataProperty { get; } = DependencyProperty.Register(
        nameof(IconData), typeof(Geometry), typeof(RadialActionMenuItem), new PropertyMetadata(null));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public static DependencyProperty DiscSizeProperty { get; } = DependencyProperty.Register(
        nameof(DiscSize), typeof(double), typeof(RadialActionMenuItem),
        new PropertyMetadata(52d, (s, e) => ((RadialActionMenuItem)s).OnDiscSizeChanged()));

    /// <summary>Disc diameter: 52 px with a pointer, 56 px for touch.</summary>
    public double DiscSize
    {
        get => (double)GetValue(DiscSizeProperty);
        set => SetValue(DiscSizeProperty, value);
    }

    public static DependencyProperty IconSizeProperty { get; } = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(RadialActionMenuItem), new PropertyMetadata(22d));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static DependencyProperty PlacementProperty { get; } = DependencyProperty.Register(
        nameof(Placement), typeof(LabelPlacement), typeof(RadialActionMenuItem),
        new PropertyMetadata(LabelPlacement.Left, (s, e) => ((RadialActionMenuItem)s).UpdateLabel()));

    public LabelPlacement Placement
    {
        get => (LabelPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static DependencyProperty IsHighlightedProperty { get; } = DependencyProperty.Register(
        nameof(IsHighlighted), typeof(bool), typeof(RadialActionMenuItem),
        new PropertyMetadata(false, (s, e) => ((RadialActionMenuItem)s).UpdateHighlight(true)));

    /// <summary>True while a finger sliding from the trigger is over this item: the disc fills with ink.</summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>The data item this disc shows.</summary>
    public RadialMenuItemData? Data { get; internal set; }

    internal static Geometry? ParseIcon(string? data)
        => string.IsNullOrWhiteSpace(data) ? null : (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);

    private void OnDiscSizeChanged() => UpdateLabel();

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _disc = GetTemplateChild(DiscPart) as FrameworkElement;
        UpdateLabel();
        UpdateHighlight(false);
    }

    private void UpdateLabel()
    {
        var state = Placement == LabelPlacement.BadgeOnly ? "BadgeOnly" : "Label" + Placement;
        VisualStateManager.GoToState(this, state, false);
    }

    private void UpdateHighlight(bool useTransitions)
        => VisualStateManager.GoToState(this, IsHighlighted ? "Highlighted" : "NotHighlighted", useTransitions);
}
