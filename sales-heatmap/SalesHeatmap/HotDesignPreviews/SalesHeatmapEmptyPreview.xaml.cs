using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("No sales in filter", typeof(global::SalesHeatmap.Controls.SalesHeatmapWidget))]
public sealed partial class SalesHeatmapEmptyPreview : Preview
{
    public SalesHeatmapEmptyPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => SalesPreviewData.Sample();
}
