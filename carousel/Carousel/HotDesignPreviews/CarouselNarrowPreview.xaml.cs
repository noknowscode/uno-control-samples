using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plates, narrow (390 px)", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselNarrowPreview : Preview
{
    public CarouselNarrowPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => PreviewData.Plates();
}
