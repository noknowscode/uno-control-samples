using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Touch: slide to an item, its name shows", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialTouchSlidePreview : Preview
{
    public RadialTouchSlidePreview()
    {
        this.InitializeComponent();
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: true);
}
