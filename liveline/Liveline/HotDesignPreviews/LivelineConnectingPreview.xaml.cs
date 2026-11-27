using Uno.UI.HotDesign;

namespace Liveline.Demo.HotDesignPreviews;

[Preview("Connecting", typeof(global::Liveline.LivelineChart))]
public sealed partial class LivelineConnectingPreview : Preview
{
    public LivelineConnectingPreview() => this.InitializeComponent();

    protected override object? LoadDataContext() => null;
}
