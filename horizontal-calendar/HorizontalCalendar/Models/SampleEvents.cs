namespace HorizontalCalendar.Models;

/// <summary>
/// The Control Gallery sheet's three weeks of events and closed days, anchored so that the sheet's
/// "Monday 23" is the Monday of the current week. Used by the demo page and the Hot Design previews.
/// </summary>
public static class SampleEvents
{
    // Day offset from the current week's Monday, start, duration in minutes, title, detail, location.
    private static readonly (int Day, string Start, int Minutes, string Title, string Detail, string Location)[] Rows =
    [
        (-7, "10:00 AM", 60, "Sprint planning", "Scope for the two-week cycle", "Teams"),
        (-6, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (-6, "3:00 PM", 45, "Design review", "Onboarding flow, round two", "Studio"),
        (-5, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (-4, "1:00 PM", 60, "Vendor call", "Renewal terms for the parts catalogue", "Zoom"),
        (-4, "4:00 PM", 30, "1:1 with Léa", "Quarter goals", "Office"),
        (-2, "10:30 AM", 120, "Sauce day prep", "Jars, lids and the big pot", "Home"),
        (0, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (0, "11:00 AM", 60, "Roadmap review", "Q1 themes and owners", "Room 4"),
        (0, "2:30 PM", 60, "Customer interview", "Migration from WPF", "Zoom"),
        (1, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (1, "1:00 PM", 90, "Workshop", "Gallery art direction", "Studio"),
        (2, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (2, "10:00 AM", 60, "Release check", "Build 7.0.2 sign-off", "Online"),
        (2, "3:00 PM", 60, "Content sync", "December blog calendar", "Teams"),
        (3, "9:00 AM", 30, "Morning standup", "Blockers and priorities for the day", "Teams"),
        (3, "11:00 AM", 60, "Client strategy call", "Q1 roadmap and deliverables", "Zoom"),
        (3, "12:30 PM", 60, "Team lunch", "The new place on Rue Notre-Dame", "Downtown"),
        (3, "2:00 PM", 90, "Code review", "Pull requests for the auth module", "Online"),
        (6, "5:00 PM", 120, "Hockey", "Dorval arena, rink 2", "Dorval"),
        (7, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (7, "2:00 PM", 60, "Partner demo", "Uno Platform Studio walkthrough", "Zoom"),
        (9, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (9, "11:00 AM", 60, "Hiring panel", "Senior developer, second round", "Room 2"),
        (9, "4:00 PM", 30, "Retro", "What went well in November", "Teams"),
        (10, "10:00 AM", 60, "Budget review", "2027 tooling and licences", "Office"),
        (11, "9:00 AM", 30, "Morning standup", "Blockers and priorities", "Teams"),
        (11, "1:00 PM", 60, "Workshop", "Accessibility audit, part one", "Studio"),
    ];

    /// <summary>The Monday the sample week starts on.</summary>
    public static DateTime WeekStart(DateTime today) => today.Date.AddDays(-(((int)today.DayOfWeek + 6) % 7));

    public static List<CalendarEvent> Create() => Create(DateTime.Today);

    public static List<CalendarEvent> Create(DateTime today)
    {
        var monday = WeekStart(today);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        return Rows.Select(r =>
        {
            var start = monday.AddDays(r.Day) + DateTime.Parse(r.Start, culture).TimeOfDay;
            return new CalendarEvent
            {
                Title = r.Title,
                Description = r.Detail,
                Location = r.Location,
                StartTime = start,
                EndTime = start.AddMinutes(r.Minutes),
            };
        }).ToList();
    }

    /// <summary>The sheet's closed days: Friday of this week ("Office closed") and next Tuesday ("Booked out").</summary>
    public static Dictionary<DateTime, CalendarUnavailableDay> UnavailableDays(DateTime today)
    {
        var monday = WeekStart(today);
        return new()
        {
            [monday.AddDays(4)] = new("Closed", "Office closed"),
            [monday.AddDays(8)] = new("Full", "Booked out"),
        };
    }
}
