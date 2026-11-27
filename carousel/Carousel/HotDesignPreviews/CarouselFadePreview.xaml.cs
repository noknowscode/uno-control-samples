using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Fade transition, no looping", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselFadePreview : Preview
{
    public CarouselFadePreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => PreviewData.Plates();
}
