using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("Ready, 390 px (compact)", typeof(global::SalesHeatmap.Controls.SalesHeatmapWidget))]
public sealed partial class SalesHeatmapCompactPreview : Preview
{
    public SalesHeatmapCompactPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => SalesPreviewData.Sample();
}
