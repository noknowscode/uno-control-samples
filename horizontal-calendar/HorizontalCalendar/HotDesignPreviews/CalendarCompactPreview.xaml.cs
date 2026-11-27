using Uno.UI.HotDesign;

namespace HorizontalCalendar.HotDesignPreviews;

[Preview("Phone layout (390 px)", typeof(global::HorizontalCalendar.Controls.HorizontalCalendar))]
public sealed partial class CalendarCompactPreview : Preview
{
    public CalendarCompactPreview()
    {
        this.InitializeComponent();
        PreviewData.Configure(Calendar);
    }
}
