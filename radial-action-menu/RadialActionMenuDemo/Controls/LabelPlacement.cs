namespace RadialActionMenuDemo.Controls;

/// <summary>Where an item's label pill sits relative to its disc. The menu sets it from the arc.</summary>
public enum LabelPlacement
{
    Left,
    Right,
    Above,
    Below,
    /// <summary>No pill: only the key badge on the disc's top-left corner (touch layout).</summary>
    BadgeOnly,
}
