using Uno.UI.HotDesign;

namespace Liveline.Demo.HotDesignPreviews;

[Preview("Paused", typeof(global::Liveline.LivelineChart))]
public sealed partial class LivelinePausedPreview : Preview
{
    public LivelinePausedPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => LivelinePreviewData.Walk();
}
