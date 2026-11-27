using Uno.UI.HotDesign;

namespace HorizontalCalendar.HotDesignPreviews;

[Preview("Loading", typeof(global::HorizontalCalendar.Controls.HorizontalCalendar))]
public sealed partial class CalendarLoadingPreview : Preview
{
    public CalendarLoadingPreview()
    {
        this.InitializeComponent();
        PreviewData.Configure(Calendar);
    }
}
