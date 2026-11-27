using Uno.UI.HotDesign;

namespace SnackbarDemo.HotDesignPreviews;

[Preview("Long message (wraps at 44 characters, action below)", typeof(global::Uno.Toolkit.UI.Snackbar))]
public sealed partial class SnackbarLongMessagePreview : Preview
{
    public SnackbarLongMessagePreview() => this.InitializeComponent();
}
