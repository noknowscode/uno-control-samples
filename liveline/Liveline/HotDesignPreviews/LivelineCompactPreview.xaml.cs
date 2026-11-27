using Uno.UI.HotDesign;

namespace Liveline.Demo.HotDesignPreviews;

[Preview("Live, 390 px (compact)", typeof(global::Liveline.LivelineChart))]
public sealed partial class LivelineCompactPreview : Preview
{
    public LivelineCompactPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => LivelinePreviewData.Walk();
}
