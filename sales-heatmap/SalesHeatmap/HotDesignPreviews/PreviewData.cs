using SalesHeatmap.Controls;

namespace SalesHeatmap.HotDesignPreviews;

/// <summary>What the widget previews bind to: the prototype's Q4 2026 sample, updated at 6:40 PM.</summary>
public sealed record SalesPreviewData(SalesSnapshot Snapshot)
{
    public static SalesPreviewData Sample()
        => new(SalesSnapshot.Q4Sample(new DateTimeOffset(DateTime.Today.AddHours(18).AddMinutes(40))));
}
