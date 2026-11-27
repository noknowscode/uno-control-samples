using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plate: image failed", typeof(global::Uno.Toolkit.UI.CarouselPlate))]
public sealed partial class CarouselPlateFailedPreview : Preview
{
    public CarouselPlateFailedPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => null;
}
