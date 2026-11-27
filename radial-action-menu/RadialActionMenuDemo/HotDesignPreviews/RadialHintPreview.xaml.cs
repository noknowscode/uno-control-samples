using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Closed with the Space hint (hover or keyboard focus)", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialHintPreview : Preview
{
    public RadialHintPreview()
    {
        this.InitializeComponent();
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: true);
}
