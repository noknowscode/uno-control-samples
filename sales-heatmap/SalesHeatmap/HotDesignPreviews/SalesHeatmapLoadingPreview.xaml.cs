using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("Loading", typeof(global::SalesHeatmap.Controls.SalesHeatmapWidget))]
public sealed partial class SalesHeatmapLoadingPreview : Preview
{
    public SalesHeatmapLoadingPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => null;
}
