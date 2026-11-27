using Uno.UI.HotDesign;

namespace AdaptiveInputDemo.HotDesignPreviews;

[Preview("Accepted: a token; Backspace undoes", typeof(global::AdaptiveInputDemo.Controls.AdaptiveInputControl))]
public sealed partial class InputAcceptedPreview : Preview
{
    public InputAcceptedPreview()
    {
        this.InitializeComponent();
        // "Today" as on the design sheet, so relative dates read the same.
        Input.ReferenceDate = new DateTime(2026, 11, 25);
        Loaded += (_, _) => Input.Accept();
    }
}
