using Uno.UI.HotDesign;

namespace Liveline.Demo.HotDesignPreviews;

[Preview("Disconnected", typeof(global::Liveline.LivelineChart))]
public sealed partial class LivelineDisconnectedPreview : Preview
{
    public LivelineDisconnectedPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => LivelinePreviewData.Walk();
}
