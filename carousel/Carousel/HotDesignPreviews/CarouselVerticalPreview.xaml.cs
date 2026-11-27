using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Vertical", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselVerticalPreview : Preview
{
    public CarouselVerticalPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => PreviewData.Plates();
}
