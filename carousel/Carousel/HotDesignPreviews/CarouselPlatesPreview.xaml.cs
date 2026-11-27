using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plates, side caption (default)", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselPlatesPreview : Preview
{
    public CarouselPlatesPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => PreviewData.Plates();
}
