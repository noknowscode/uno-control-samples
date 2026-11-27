using Microsoft.UI.Xaml;
using SkiaSharp;

namespace PrecisionDial.Controls;

/// <summary>
/// The colour the Skia renderers use for marks drawn on the page around the knob (detent ticks, the unlit arc
/// segments, the menu icons): white on the dark stage, near-black on the light one. The knob itself is dark metal in
/// both themes, so its own highlights stay white.
/// </summary>
internal static class DialInk
{
    public static SKColor For(ElementTheme theme)
        => theme == ElementTheme.Light ? new SKColor(28, 27, 31) : SKColors.White;
}
