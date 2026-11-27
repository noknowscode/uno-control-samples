using Microsoft.UI.Xaml.Input;

namespace SalesHeatmap.Controls;

/// <summary>
/// One day of the <see cref="SalesHeatmapWidget"/> grid. Its look is the default style in Themes/SalesHeatmap.xaml,
/// driven by four visual state groups: KindStates (Void, Level1–Level5, Closed, NoData, Ahead, Skeleton),
/// PeriodStates (InPeriod, OutOfPeriod at 22%), SelectionStates and CommonStates (the hover ring).
/// </summary>
public sealed partial class SalesHeatmapCell : Control
{
    private bool _pointerOver;

    public SalesHeatmapCell()
    {
        DefaultStyleKey = typeof(SalesHeatmapCell);
        IsTabStop = false;
    }

    /// <summary>The day this cell shows.</summary>
    public SalesDay? Day { get; internal set; }

    /// <summary>The cell's position in the 98-day grid (column-major).</summary>
    public int Index { get; internal set; }

    internal string KindState { get; private set; } = "Void";

    internal bool IsInPeriod { get; private set; } = true;

    internal bool IsSelected { get; private set; }

    internal bool IsInteractive => Day is { Kind: not (SalesDayKind.Void or SalesDayKind.Ahead) } && KindState != "Skeleton";

    internal void Apply(string kindState, bool inPeriod, bool selected, bool useTransitions)
    {
        KindState = kindState;
        IsInPeriod = inPeriod;
        IsSelected = selected;
        GoToStates(useTransitions);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        GoToStates(useTransitions: false);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _pointerOver = true;
        GoToStates(useTransitions: true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        GoToStates(useTransitions: true);
    }

    private void GoToStates(bool useTransitions)
    {
        VisualStateManager.GoToState(this, KindState, useTransitions);
        VisualStateManager.GoToState(this, IsInPeriod ? "InPeriod" : "OutOfPeriod", useTransitions);
        VisualStateManager.GoToState(this, IsSelected ? "Selected" : "Unselected", useTransitions);
        VisualStateManager.GoToState(this, _pointerOver && IsInteractive && !IsSelected ? "PointerOver" : "Normal", useTransitions);
    }
}
