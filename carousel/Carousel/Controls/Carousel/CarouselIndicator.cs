using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Uno.Toolkit.UI;

public partial class CarouselIndicator : Panel
{
    private int _itemCount;
    private int _selectedIndex;
    private const double DotSize = 8;
    private const double DotSpacing = 8;
    private Orientation _orientation = Orientation.Horizontal;

    private static readonly Brush DefaultActiveBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush DefaultInactiveBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));

    private Brush EffectiveActiveBrush => ActiveBrush ?? DefaultActiveBrush;
    private Brush EffectiveInactiveBrush => InactiveBrush ?? DefaultInactiveBrush;

    internal int ItemCount
    {
        get => _itemCount;
        set { if (_itemCount != value) { _itemCount = value; RebuildDots(); } }
    }

    internal int SelectedIndex
    {
        get => _selectedIndex;
        set { if (_selectedIndex != value) { _selectedIndex = value; UpdateDotStates(); } }
    }

    public static DependencyProperty ActiveBrushProperty { get; } = DependencyProperty.Register(
        nameof(ActiveBrush), typeof(Brush), typeof(CarouselIndicator),
        new PropertyMetadata(null, (d, _) => ((CarouselIndicator)d).UpdateDotStates()));

    public static DependencyProperty InactiveBrushProperty { get; } = DependencyProperty.Register(
        nameof(InactiveBrush), typeof(Brush), typeof(CarouselIndicator),
        new PropertyMetadata(null, (d, _) => ((CarouselIndicator)d).UpdateDotStates()));

    /// <summary>Fill of the current page's dot. The template sets it with a ThemeResource so it follows the theme.</summary>
    public Brush? ActiveBrush
    {
        get => (Brush?)GetValue(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    /// <summary>Fill of the other dots.</summary>
    public Brush? InactiveBrush
    {
        get => (Brush?)GetValue(InactiveBrushProperty);
        set => SetValue(InactiveBrushProperty, value);
    }

    internal Orientation Orientation
    {
        get => _orientation;
        set { if (_orientation != value) { _orientation = value; InvalidateMeasure(); } }
    }

    private void RebuildDots()
    {
        Children.Clear();
        for (int i = 0; i < _itemCount; i++)
        {
            Children.Add(new Ellipse
            {
                Width = DotSize, Height = DotSize,
                Fill = i == _selectedIndex ? EffectiveActiveBrush : EffectiveInactiveBrush
            });
        }
        InvalidateMeasure();
    }

    private void UpdateDotStates()
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is Ellipse dot)
            {
                dot.Fill = i == _selectedIndex ? EffectiveActiveBrush : EffectiveInactiveBrush;
            }
        }
    }

    // All dots are identical size, so we pre-compute totals instead of measuring each child.
    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Children.Count;
        if (count == 0) return default;

        foreach (var child in Children)
            child.Measure(new Size(DotSize, DotSize));

        var totalMain = count * DotSize + Math.Max(0, count - 1) * DotSpacing;
        return _orientation == Orientation.Horizontal
            ? new Size(totalMain, DotSize)
            : new Size(DotSize, totalMain);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0) return finalSize;

        var totalMain = count * DotSize + Math.Max(0, count - 1) * DotSpacing;
        var horizontal = _orientation == Orientation.Horizontal;
        var offset = Math.Max(0, ((horizontal ? finalSize.Width : finalSize.Height) - totalMain) / 2.0);

        foreach (var child in Children)
        {
            if (horizontal)
            {
                var y = (finalSize.Height - DotSize) / 2.0;
                child.Arrange(new Rect(offset, y, DotSize, DotSize));
            }
            else
            {
                var x = (finalSize.Width - DotSize) / 2.0;
                child.Arrange(new Rect(x, offset, DotSize, DotSize));
            }
            offset += DotSize + DotSpacing;
        }
        return finalSize;
    }
}
