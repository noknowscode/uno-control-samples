using Uno.UI.HotDesign;

namespace SalesHeatmap.HotDesignPreviews;

[Preview("Overview page: loading, then ready", typeof(global::SalesHeatmap.Presentation.MainPage))]
public sealed partial class MainPagePreview : Preview
{
    public MainPagePreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => new MainViewModel();
}
