using Uno.UI.HotDesign;

namespace CarouselDemo.HotDesignPreviews;

[Preview("Plate: long title", typeof(global::Uno.Toolkit.UI.CarouselPlate))]
public sealed partial class CarouselPlateLongTitlePreview : Preview
{
    public CarouselPlateLongTitlePreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => null;
}
