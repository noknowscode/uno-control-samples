using SalesHeatmap.Controls;

namespace SalesHeatmap.Presentation;

/// <summary>
/// The overview's data: the prototype's Q4 2026 sample after a short load, plus a widget state and a filter the demo
/// can switch to show the Stale and Empty states. Retry and Clear filters bring the widget back to Ready.
/// </summary>
public partial record MainModel
{
    private static readonly TimeSpan LoadDelay = TimeSpan.FromSeconds(1.2);

    public IFeed<SalesSnapshot> Snapshot => Feed.Async(async ct =>
    {
        await Task.Delay(LoadDelay, ct);
        var snapshot = SalesSnapshot.Q4Sample(DateTimeOffset.Now);
        await Status.SetAsync(SalesHeatmapState.Ready, ct);
        return snapshot;
    });

    public IState<SalesHeatmapState> Status => State.Value(this, () => SalesHeatmapState.Loading);

    public IState<string> Filter => State.Value(this, () => string.Empty);

    /// <summary>Simulates a failed refresh: the widget keeps the last grid and says how old it is.</summary>
    public async ValueTask SimulateStale(CancellationToken ct) => await Status.SetAsync(SalesHeatmapState.Stale, ct);

    /// <summary>Simulates a filter that matches no sales.</summary>
    public async ValueTask SimulateEmpty(CancellationToken ct)
    {
        await Filter.SetAsync("Region: Canada · Online", ct);
        await Status.SetAsync(SalesHeatmapState.Empty, ct);
    }

    public async ValueTask Retry(CancellationToken ct) => await Status.SetAsync(SalesHeatmapState.Ready, ct);

    public async ValueTask ClearFilters(CancellationToken ct)
    {
        await Filter.SetAsync(string.Empty, ct);
        await Status.SetAsync(SalesHeatmapState.Ready, ct);
    }
}
