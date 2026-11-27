using RadialActionMenuDemo.Controls;
using Uno.UI.HotDesign;

namespace RadialActionMenuDemo.HotDesignPreviews;

[Preview("Opening (30 ms stagger, 320 ms settle)", typeof(global::RadialActionMenuDemo.Controls.RadialActionMenu))]
public sealed partial class RadialOpeningPreview : Preview
{
    public RadialOpeningPreview()
    {
        this.InitializeComponent();
        Loaded += async (_, _) =>
        {
            await Task.Delay(400);
            Menu.IsOpen = true;
        };
    }

    protected override object? LoadDataContext() => RadialMenuIcons.ContactActions(callAvailable: true);
}
