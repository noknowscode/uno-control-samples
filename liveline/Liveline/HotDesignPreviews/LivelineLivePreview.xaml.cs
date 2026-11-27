using Uno.UI.HotDesign;

namespace Liveline.Demo.HotDesignPreviews;

[Preview("Live", typeof(global::Liveline.LivelineChart))]
public sealed partial class LivelineLivePreview : Preview
{
    public LivelineLivePreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => LivelinePreviewData.Walk();
}
