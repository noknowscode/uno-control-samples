using Windows.Foundation;

namespace Uno.Toolkit.UI;

/// <summary>
/// One row of thumbnails, spread so the first starts at the left edge and the last ends at the right edge
/// (CSS <c>justify-content: space-between</c>), with at least <see cref="MinSpacing"/> between them.
/// </summary>
public partial class CarouselThumbnailPanel : Panel
{
    public static DependencyProperty MinSpacingProperty { get; } = DependencyProperty.Register(
        nameof(MinSpacing), typeof(double), typeof(CarouselThumbnailPanel),
        new PropertyMetadata(10d, (d, _) => ((CarouselThumbnailPanel)d).InvalidateMeasure()));

    public double MinSpacing
    {
        get => (double)GetValue(MinSpacingProperty);
        set => SetValue(MinSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        width += Math.Max(0, Children.Count - 1) * MinSpacing;
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Max(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = Children.Count;
        if (count == 0) return finalSize;

        var content = Children.Sum(c => c.DesiredSize.Width);
        var gap = count > 1 ? Math.Max(MinSpacing, (finalSize.Width - content) / (count - 1)) : 0;
        double x = 0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + gap;
        }
        return finalSize;
    }
}
