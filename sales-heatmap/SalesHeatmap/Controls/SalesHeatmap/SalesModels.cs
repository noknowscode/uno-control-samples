namespace SalesHeatmap.Controls;

/// <summary>What a heatmap cell holds.</summary>
public enum SalesDayKind
{
    /// <summary>Outside the quarter: the cell keeps its place but is not drawn.</summary>
    Void,
    /// <summary>A day with sales.</summary>
    Data,
    /// <summary>The store was closed: outlined, revenue 0.</summary>
    Closed,
    /// <summary>Nothing recorded: hatched, never drawn as zero.</summary>
    NoData,
    /// <summary>After today: dashed outline.</summary>
    Ahead,
}

/// <summary>The states a <see cref="SalesHeatmapWidget"/> shows.</summary>
public enum SalesHeatmapState
{
    /// <summary>Waiting for data: the grid, figures and rows keep their size as outlines and skeletons.</summary>
    Loading,
    /// <summary>Live data.</summary>
    Ready,
    /// <summary>The filter matches no sales: says so in place and never draws zeros.</summary>
    Empty,
    /// <summary>The last refresh failed: keeps the last good grid and says how old it is.</summary>
    Stale,
}

/// <summary>One day of the grid.</summary>
/// <remarks>Settable properties: the XAML compiler writes metadata for list item types, and init-only setters fail there.</remarks>
public sealed record SalesDay(DateTime Date, SalesDayKind Kind, double? Revenue, int Orders)
{
    public DateTime Date { get; set; } = Date;
    public SalesDayKind Kind { get; set; } = Kind;
    public double? Revenue { get; set; } = Revenue;
    public int Orders { get; set; } = Orders;
}

/// <summary>A region's share of the selected period's revenue, and its change.</summary>
public sealed record SalesRegionShare(string Name, double Share, double Change)
{
    public string Name { get; set; } = Name;
    public double Share { get; set; } = Share;
    public double Change { get; set; } = Change;
}

/// <summary>How a period compares: a change in percent (null for none) and what it is compared with.</summary>
public sealed record SalesComparison(double? Change, string Note)
{
    public double? Change { get; set; } = Change;
    public string Note { get; set; } = Note;
}

/// <summary>
/// Everything the widget shows: 14 weeks of days starting on a Monday (column-major, as the grid is drawn), the
/// quarter they cover, today, the regions' shares and one comparison per period (three months, then the quarter).
/// </summary>
public sealed record SalesSnapshot(
    DateTime QuarterStart,
    DateTime Today,
    IReadOnlyList<SalesDay> Days,
    IReadOnlyList<SalesRegionShare> Regions,
    IReadOnlyList<SalesComparison> Comparisons,
    DateTimeOffset UpdatedAt)
{
    public DateTime QuarterStart { get; set; } = QuarterStart;
    public DateTime Today { get; set; } = Today;
    public IReadOnlyList<SalesDay> Days { get; set; } = Days;
    public IReadOnlyList<SalesRegionShare> Regions { get; set; } = Regions;
    public IReadOnlyList<SalesComparison> Comparisons { get; set; } = Comparisons;
    public DateTimeOffset UpdatedAt { get; set; } = UpdatedAt;

    /// <summary>
    /// The prototype's sample: Q4 2026 seen on Nov 25, from Sep 28 (a Monday) for 98 days. Revenue is
    /// 9200 + 12k + 2100 sin(0.45k) + 1100 sin(1.9k + 1), weekends at 55%, Nov 13 at 155%, at least 1500; Oct 12 is
    /// closed, Oct 20–21 have no data, and orders are revenue / 295.
    /// </summary>
    public static SalesSnapshot Q4Sample(DateTimeOffset updatedAt)
    {
        var start = new DateTime(2026, 9, 28);
        var q0 = new DateTime(2026, 10, 1);
        var q1 = new DateTime(2026, 12, 31);
        var today = new DateTime(2026, 11, 25);
        var days = new List<SalesDay>(98);
        for (var i = 0; i < 98; i++)
        {
            var d = start.AddDays(i);
            var inQuarter = d >= q0 && d <= q1;
            var k = (d - q0).Days;
            var weekday = ((int)d.DayOfWeek + 6) % 7;
            var v = 9200 + k * 12 + 2100 * Math.Sin(k * 0.45) + 1100 * Math.Sin(k * 1.9 + 1);
            if (weekday >= 5)
            {
                v *= 0.55;
            }

            if (d.Month == 11 && d.Day == 13)
            {
                v *= 1.55;
            }

            v = Math.Round(Math.Max(1500, v));
            var kind = !inQuarter ? SalesDayKind.Void
                : d > today ? SalesDayKind.Ahead
                : d == new DateTime(2026, 10, 12) ? SalesDayKind.Closed
                : d == new DateTime(2026, 10, 20) || d == new DateTime(2026, 10, 21) ? SalesDayKind.NoData
                : SalesDayKind.Data;
            days.Add(kind switch
            {
                SalesDayKind.Data => new SalesDay(d, kind, v, (int)Math.Round(v / 295)),
                SalesDayKind.Closed => new SalesDay(d, kind, 0, 0),
                _ => new SalesDay(d, kind, null, 0),
            });
        }

        // November to date against October 1–25, as the prototype computes it.
        double Sum(int month, int cut) => days
            .Where(x => x.Date.Month == month && x.Revenue is not null && x.Date.Day <= cut && x.Kind != SalesDayKind.Void)
            .Sum(x => x.Revenue!.Value);
        var november = Sum(11, 31);
        var octoberToDay = Sum(10, 25);

        return new SalesSnapshot(
            q0,
            today,
            days,
            [new("New York", 0.33, 14.5), new("Los Angeles", 0.26, -5.0), new("Canada", 0.19, -1.7)],
            [
                new(3.2, "vs September"),
                new((november - octoberToDay) / octoberToDay * 100, "vs Oct 1–25"),
                new(null, "December starts in 6 days"),
                new(7.1, "vs Q3, same days"),
            ],
            updatedAt);
    }
}
