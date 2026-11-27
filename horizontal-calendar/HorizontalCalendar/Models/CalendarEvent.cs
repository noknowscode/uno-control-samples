namespace HorizontalCalendar.Models;

/// <summary>An event on the calendar.</summary>
public class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;

    /// <summary>One line under the title in the agenda.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>A place or a meeting service ("Teams", "Zoom" and "Online" get a video icon on phones).</summary>
    public string Location { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}

/// <summary>Why a day can't be booked: a short label under the number ("Closed") and the full reason.</summary>
public sealed record CalendarUnavailableDay(string Label, string Reason);
