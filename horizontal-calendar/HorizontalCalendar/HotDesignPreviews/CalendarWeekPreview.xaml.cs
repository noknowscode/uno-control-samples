using Uno.UI.HotDesign;

namespace HorizontalCalendar.HotDesignPreviews;

[Preview("Week with events (default)", typeof(global::HorizontalCalendar.Controls.HorizontalCalendar))]
public sealed partial class CalendarWeekPreview : Preview
{
    public CalendarWeekPreview()
    {
        this.InitializeComponent();
        PreviewData.Configure(Calendar);
    }
}
