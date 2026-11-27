using Uno.UI.HotDesign;

namespace HorizontalCalendar.HotDesignPreviews;

[Preview("Load error", typeof(global::HorizontalCalendar.Controls.HorizontalCalendar))]
public sealed partial class CalendarErrorPreview : Preview
{
    public CalendarErrorPreview()
    {
        this.InitializeComponent();
        PreviewData.Configure(Calendar);
    }
}
