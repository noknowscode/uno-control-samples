using System.Collections.Generic;
using PrecisionDial.Controls;
using Uno.UI.HotDesign;

namespace PrecisionDial.HotDesignPreviews;

[Preview("Menu mode", typeof(global::PrecisionDial.Controls.PrecisionDial))]
public sealed partial class DialMenuModePreview : Preview
{
    public DialMenuModePreview()
    {
        this.InitializeComponent();
        Dial.MenuItems = new List<DialMenuItem>
        {
            new() { Label = "MUSIC", Icon = "\uE33C", Tag = "music" },
            new() { Label = "RADIO", Icon = "\uE77E", Tag = "radio" },
            new() { Label = "PODCAST", Icon = "\uE326", Tag = "podcast" },
            new() { Label = "VIDEO", Icon = "\uE4DA", Tag = "video" },
            new() { Label = "SETTINGS", Icon = "\uE270", Tag = "settings" },
        };
    }
}
