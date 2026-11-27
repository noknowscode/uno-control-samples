using System.Windows.Input;

namespace RadialActionMenuDemo.Controls;

/// <summary>One action on the radial menu.</summary>
public class RadialMenuItemData
{
    /// <summary>Icon outline as path data on a 24 px grid (Tabler-style, drawn with a 1.75 stroke).</summary>
    public string? Icon { get; set; }

    /// <summary>The action's name: shown in the label pill and the touch readout, and read by screen readers.</summary>
    public string Label { get; set; } = string.Empty;

    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }

    /// <summary>False shows the item dimmed and skips it for keys, arrows and touch (for example "Call is unavailable").</summary>
    public bool IsEnabled { get; set; } = true;
}
