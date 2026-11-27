using Uno.UI.HotDesign;

namespace AdaptiveInputDemo.HotDesignPreviews;

[Preview("Plain text", typeof(global::AdaptiveInputDemo.Controls.AdaptiveInputControl))]
public sealed partial class InputPlainPreview : Preview
{
    public InputPlainPreview()
    {
        this.InitializeComponent();
        // "Today" as on the design sheet, so relative dates read the same.
        Input.ReferenceDate = new DateTime(2026, 11, 25);
    }
}
