using Microsoft.UI.Xaml.Automation.Peers;

namespace Uno.Toolkit.UI;

/// <summary>
/// Automation peer for the <see cref="Carousel"/> control.
/// </summary>
public class CarouselAutomationPeer : FrameworkElementAutomationPeer
{
    private Carousel OwnerCarousel => (Carousel)Owner;

    public CarouselAutomationPeer(Carousel owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(Carousel);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.List;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrEmpty(name)) return name;

        return "Carousel";
    }

    protected override string GetItemStatusCore()
    {
        var carousel = OwnerCarousel;
        return carousel.ItemCount == 0 ? string.Empty : $"{carousel.SelectedIndex + 1} of {carousel.ItemCount}";
    }
}
