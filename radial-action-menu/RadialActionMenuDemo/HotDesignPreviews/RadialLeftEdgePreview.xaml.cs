using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Left edge: arc mirrors, Call unavailable", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialLeftEdgePreview : Preview
{
    public RadialLeftEdgePreview()
    {
        this.InitializeComponent();
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: false);
}
