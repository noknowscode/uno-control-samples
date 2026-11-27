using Windows.Foundation;

namespace Uno.Toolkit.UI;

/// <summary>
/// Lays out the carousel's items. In overlay mode every item sits at (0,0) and the Carousel shows one at a time
/// through opacity and Canvas.ZIndex. In track mode (a peek or a spacing is set) the items sit in a row, each one
/// <c>extent - peek - spacing</c> long, and the Carousel moves the whole panel with its RenderTransform.
/// </summary>
internal partial class CarouselPanel : Panel
{
    private double _peek;
    private double _spacing;
    private Orientation _orientation = Orientation.Horizontal;

    internal bool IsTrack => _peek > 0 || _spacing > 0;

    /// <summary>Distance between the starts of two neighbouring items in track mode.</summary>
    internal double Pitch { get; private set; }

    internal void Configure(double peek, double spacing, Orientation orientation)
    {
        if (_peek == peek && _spacing == spacing && _orientation == orientation) return;
        _peek = Math.Max(0, peek);
        _spacing = Math.Max(0, spacing);
        _orientation = orientation;
        InvalidateMeasure();
    }

    private Size ItemSize(Size size)
    {
        if (!IsTrack) return size;
        return _orientation == Orientation.Horizontal
            ? new Size(Math.Max(0, size.Width - _peek - _spacing), size.Height)
            : new Size(size.Width, Math.Max(0, size.Height - _peek - _spacing));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(
            double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 600 : availableSize.Height);

        var itemSize = ItemSize(size);
        foreach (var child in Children)
        {
            child.Measure(itemSize);
        }

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemSize = ItemSize(finalSize);
        var horizontal = _orientation == Orientation.Horizontal;
        Pitch = IsTrack ? (horizontal ? itemSize.Width : itemSize.Height) + _spacing : 0;

        for (int i = 0; i < Children.Count; i++)
        {
            var offset = i * Pitch;
            Children[i].Arrange(horizontal
                ? new Rect(offset, 0, itemSize.Width, itemSize.Height)
                : new Rect(0, offset, itemSize.Width, itemSize.Height));
        }

        return finalSize;
    }
}
