using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace ReticleLab.Controls;

/// <summary>
/// Turns a 0–100 value into the compact ring's arc. The arc starts at 150° (lower left, as in the prototype's row
/// rings) and runs clockwise 3.6° per percent. ConverterParameter is "centerX,centerY,radius" in the ring's design
/// units. A ProgressRing template cannot do this math itself, so the compact style binds Value through it.
/// </summary>
public sealed partial class ReticleArcConverter : IValueConverter
{
    private const double StartDegrees = 150;

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var p = value is double d && !double.IsNaN(d) ? Math.Clamp(d, 0, 100) : 0;
        var parts = (parameter as string ?? "32,32,26.9").Split(',');
        var cx = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var cy = double.Parse(parts[1], CultureInfo.InvariantCulture);
        var r = double.Parse(parts[2], CultureInfo.InvariantCulture);
        if (p <= 0)
        {
            return null;
        }

        var sweep = Math.Min(359.9, 3.6 * p);
        Point At(double degrees)
        {
            var a = degrees * Math.PI / 180;
            return new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }

        var figure = new PathFigure { StartPoint = At(StartDegrees), IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = At(StartDegrees + sweep),
            Size = new Size(r, r),
            IsLargeArc = sweep > 180,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
