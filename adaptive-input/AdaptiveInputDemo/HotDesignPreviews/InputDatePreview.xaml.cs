using Uno.UI.HotDesign;

namespace AdaptiveInputDemo.HotDesignPreviews;

[Preview("Date: tomorrow", typeof(global::AdaptiveInputDemo.Controls.AdaptiveInputControl))]
public sealed partial class InputDatePreview : Preview
{
    public InputDatePreview()
    {
        this.InitializeComponent();
        // "Today" as on the design sheet, so relative dates read the same.
        Input.ReferenceDate = new DateTime(2026, 11, 25);
    }
}
