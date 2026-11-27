using Uno.UI.HotDesign;

namespace SnackbarDemo.HotDesignPreviews;

[Preview("Held (hover or focus pauses the timer)", typeof(global::Uno.Toolkit.UI.Snackbar))]
public sealed partial class SnackbarHeldPreview : Preview
{
    public SnackbarHeldPreview() => this.InitializeComponent();
}
