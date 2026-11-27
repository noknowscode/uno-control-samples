using Uno.UI.HotDesign;

namespace AdaptiveInputDemo.HotDesignPreviews;

[Preview("Tag: #urgent", typeof(global::AdaptiveInputDemo.Controls.AdaptiveInputControl))]
public sealed partial class InputTagPreview : Preview
{
    public InputTagPreview()
    {
        this.InitializeComponent();
        // "Today" as on the design sheet, so relative dates read the same.
        Input.ReferenceDate = new DateTime(2026, 11, 25);
    }
}
