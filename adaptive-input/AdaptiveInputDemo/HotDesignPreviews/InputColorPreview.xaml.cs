using Uno.UI.HotDesign;

namespace AdaptiveInputDemo.HotDesignPreviews;

[Preview("Colour: the swatch shows the value read", typeof(global::AdaptiveInputDemo.Controls.AdaptiveInputControl))]
public sealed partial class InputColorPreview : Preview
{
    public InputColorPreview()
    {
        this.InitializeComponent();
        // "Today" as on the design sheet, so relative dates read the same.
        Input.ReferenceDate = new DateTime(2026, 11, 25);
    }
}
