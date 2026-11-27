using Windows.Foundation;

namespace HorizontalCalendar.Controls;

/// <summary>
/// Lays out three weeks of days (previous, shown, next) as 21 columns, each a seventh of the panel's width, with the
/// shown week at 0. The first child is the selection pill, placed on <see cref="PillColumn"/>; the others are the
/// days in order. The calendar clips the panel and slides it to page weeks.
/// </summary>
public partial class CalendarWeekPanel : Panel
{
    private int _pillColumn = -1;
    private double _cellInset = 4;

    internal int PillColumn
    {
        get => _pillColumn;
        set { if (_pillColumn != value) { _pillColumn = value; InvalidateArrange(); } }
    }

    /// <summary>Horizontal space left on each side of a day (4 px; 2 px on phones).</summary>
    internal double CellInset
    {
        get => _cellInset;
        set { if (_cellInset != value) { _cellInset = value; InvalidateMeasure(); } }
    }

    internal double ColumnWidth { get; private set; }

    internal Rect ColumnRect(int column, double height)
        => new((column - 7) * ColumnWidth + _cellInset, 0, Math.Max(0, ColumnWidth - 2 * _cellInset), height);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 700 : availableSize.Width;
        var cell = new Size(Math.Max(0, width / 7 - 2 * _cellInset), availableSize.Height);
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(cell);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ColumnWidth = finalSize.Width / 7;
        for (int i = 0; i < Children.Count; i++)
        {
            if (i == 0)
            {
                var pill = Children[0];
                pill.Arrange(_pillColumn >= 0 ? ColumnRect(_pillColumn, finalSize.Height) : new Rect(-10000, 0, 0, 0));
                continue;
            }
            Children[i].Arrange(ColumnRect(i - 1, finalSize.Height));
        }
        return finalSize;
    }
}
