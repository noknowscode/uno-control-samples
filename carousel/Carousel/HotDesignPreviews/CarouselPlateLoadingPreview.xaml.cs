using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plate: loading", typeof(global::Uno.Toolkit.UI.CarouselPlate))]
public sealed partial class CarouselPlateLoadingPreview : Preview
{
    public CarouselPlateLoadingPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => null;
}
