using Microsoft.UI.Xaml.Media.Animation;

namespace Uno.Toolkit.UI;

/// <summary>
/// Slides the items along the owning carousel's <see cref="Carousel.Orientation"/>.
/// </summary>
public class SlideTransition : CarouselTransitionBase
{
    protected override void BuildAnimations(Storyboard sb, UIElement? from, UIElement to, bool forward)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(400));
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var horizontal = FindCarousel(to)?.Orientation != Orientation.Vertical;
        var property = horizontal ? nameof(TranslateTransform.X) : nameof(TranslateTransform.Y);
        var size = to is FrameworkElement fe ? (horizontal ? fe.ActualWidth : fe.ActualHeight) : 0;
        var distance = size > 0 ? size : 800;

        var inStart = forward ? distance : -distance;
        EnsureTranslateTransform(to);
        SetOffset(to, inStart, horizontal);
        to.Opacity = 1.0;
        Canvas.SetZIndex(to, 1);
        AddAnimation(sb, to.RenderTransform, property, inStart, 0, duration, easing, dependent: true);

        if (from != null)
        {
            EnsureTranslateTransform(from);
            from.Opacity = 1.0;
            Canvas.SetZIndex(from, 0);
            AddAnimation(sb, from.RenderTransform, property,
                0, forward ? -distance : distance, duration, easing, dependent: true);
        }
    }

    protected override void Finalize(UIElement? from, UIElement? to)
    {
        base.Finalize(from, to);
        ResetOffset(from);
        ResetOffset(to);
    }

    private static Carousel? FindCarousel(DependencyObject element)
    {
        for (var current = VisualTreeHelper.GetParent(element); current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Carousel carousel) return carousel;
        }
        return null;
    }

    private static void SetOffset(UIElement element, double value, bool horizontal)
    {
        if (element.RenderTransform is TranslateTransform tt)
        {
            tt.X = horizontal ? value : 0;
            tt.Y = horizontal ? 0 : value;
        }
    }

    private static void ResetOffset(UIElement? element)
    {
        if (element?.RenderTransform is TranslateTransform tt) { tt.X = 0; tt.Y = 0; }
    }
}
