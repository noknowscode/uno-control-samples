using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("Stale data", typeof(global::SalesHeatmap.Controls.SalesHeatmapWidget))]
public sealed partial class SalesHeatmapStalePreview : Preview
{
    public SalesHeatmapStalePreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => SalesPreviewData.Sample();
}
