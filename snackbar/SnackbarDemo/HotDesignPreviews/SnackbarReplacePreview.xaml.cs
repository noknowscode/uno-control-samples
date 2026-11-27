using Uno.Toolkit.UI;
using Uno.UI.HotDesign;

namespace SnackbarDemo.HotDesignPreviews;

[Preview("Replace (the next one waits 160 ms, never stacks)", typeof(global::Uno.Toolkit.UI.SnackbarHost))]
public sealed partial class SnackbarReplacePreview : Preview
{
    public SnackbarReplacePreview()
    {
        this.InitializeComponent();
        Loaded += async (_, _) =>
        {
            _ = Host.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Undo, Message = "Draft deleted", ActionLabel = "Undo" });
            await Task.Delay(600);
            _ = Host.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Confirm, Message = "All changes saved" });
        };
    }
}
