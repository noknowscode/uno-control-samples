using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("Ready (Nov 24 selected)", typeof(global::SalesHeatmap.Controls.SalesHeatmapWidget))]
public sealed partial class SalesHeatmapReadyPreview : Preview
{
    public SalesHeatmapReadyPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => SalesPreviewData.Sample();
}
