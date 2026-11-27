using Liveline.Models;

namespace Liveline.Demo.HotDesignPreviews;

/// <summary>What the previews bind to: the prototype's seeded walk, 61 samples a second apart, ending now.</summary>
public sealed record LivelinePreviewData(IList<LivelinePoint> Points)
{
    public static LivelinePreviewData Walk()
    {
        long seed = 7;
        var value = 54.0;
        var end = DateTimeOffset.Now;
        var points = new List<LivelinePoint>(61);
        for (var i = 60; i >= 0; i--)
        {
            seed = seed * 16807 % 2147483647;
            var r = seed / 2147483647.0 - 0.5;
            value = Math.Clamp(value + r * 3.8 + (55 - value) * 0.06, 41, 69);
            points.Add(new LivelinePoint(end.AddSeconds(-i), value));
        }

        return new LivelinePreviewData(points);
    }
}
