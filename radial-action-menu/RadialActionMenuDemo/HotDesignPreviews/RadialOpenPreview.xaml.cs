using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Open with labels and keys", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialOpenPreview : Preview
{
    public RadialOpenPreview()
    {
        this.InitializeComponent();
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: true);
}
