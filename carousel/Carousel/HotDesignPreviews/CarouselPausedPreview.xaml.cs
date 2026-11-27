using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plates, paused", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselPausedPreview : Preview
{
    public CarouselPausedPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => PreviewData.Plates();
}
