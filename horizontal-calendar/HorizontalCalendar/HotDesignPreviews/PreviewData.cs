using HorizontalCalendar.Models;

namespace HorizontalCalendar.HotDesignPreviews;

internal static class PreviewData
{
    /// <summary>Gives a preview's calendar the sample events and closed days, and selects a day of this week.</summary>
    public static void Configure(Controls.HorizontalCalendar calendar, int? selectDayOffset = null)
    {
        var today = DateTime.Today;
        var monday = SampleEvents.WeekStart(today);
        var events = SampleEvents.Create(today);
        calendar.MinDate = monday.AddDays(-7);
        calendar.MaxDate = monday.AddDays(13);
        calendar.UnavailableDays = SampleEvents.UnavailableDays(today);
        calendar.EventsProvider = (start, end) => events.Where(e => e.StartTime.Date >= start && e.StartTime.Date <= end);
        calendar.RefreshEvents();
        if (selectDayOffset is int offset) calendar.SelectDate(monday.AddDays(offset));
    }
}
