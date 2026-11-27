using Uno.UI.HotDesign;

namespace HorizontalCalendar.HotDesignPreviews;

[Preview("Empty day", typeof(global::HorizontalCalendar.Controls.HorizontalCalendar))]
public sealed partial class CalendarEmptyDayPreview : Preview
{
    public CalendarEmptyDayPreview()
    {
        this.InitializeComponent();
        PreviewData.Configure(Calendar, selectDayOffset: 5);
    }
}
