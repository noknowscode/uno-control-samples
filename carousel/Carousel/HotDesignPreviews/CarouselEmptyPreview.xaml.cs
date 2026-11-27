using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Empty", typeof(global::Uno.Toolkit.UI.Carousel))]
public sealed partial class CarouselEmptyPreview : Preview
{
    public CarouselEmptyPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => new List<Plate>();
}
