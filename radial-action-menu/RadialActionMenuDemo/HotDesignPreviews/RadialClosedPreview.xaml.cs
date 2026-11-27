using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Closed", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialClosedPreview : Preview
{
    public RadialClosedPreview()
    {
        this.InitializeComponent();
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: true);
}
