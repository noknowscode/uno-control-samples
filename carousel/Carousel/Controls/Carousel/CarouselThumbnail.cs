using Microsoft.UI.Xaml.Controls.Primitives;

namespace Uno.Toolkit.UI;

/// <summary>
/// One thumbnail of a <see cref="Carousel"/>'s thumbnail strip: the item's thumbnail and a 2 px progress bar.
/// <c>IsChecked</c> marks the current item; the carousel sets it (clicking never toggles it) and drives the bar.
/// </summary>
[TemplatePart(Name = PART_ProgressFill, Type = typeof(FrameworkElement))]
public partial class CarouselThumbnail : ToggleButton
{
    private const string PART_ProgressFill = "PART_ProgressFill";

    public CarouselThumbnail()
    {
        DefaultStyleKey = typeof(CarouselThumbnail);
    }

    /// <summary>Scale of the progress fill (0 empty, 1 full); the carousel animates its ScaleX.</summary>
    internal ScaleTransform ProgressScale { get; } = new() { ScaleX = 0 };

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(PART_ProgressFill) is FrameworkElement fill)
        {
            fill.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);
            fill.RenderTransform = ProgressScale;
        }
    }

    protected override void OnToggle()
    {
    }
}
