using System.Windows.Input;
using HorizontalCalendar.Models;

namespace HorizontalCalendar.Controls;

/// <summary>What the agenda under the strip shows. A Ready day without events shows the empty-day state.</summary>
public enum CalendarAgendaState
{
    Ready,
    Loading,
    Error,
}

/// <summary>Regular (card, legend, Today button) or Compact (the phone layout). Auto picks from the control's width.</summary>
public enum CalendarDisplayMode
{
    Auto,
    Regular,
    Compact,
}

public sealed class CalendarEventOptionsRequestedEventArgs(CalendarEvent calendarEvent, FrameworkElement target) : EventArgs
{
    public CalendarEvent Event { get; } = calendarEvent;

    /// <summary>The "More options" button, to place a flyout on.</summary>
    public FrameworkElement Target { get; } = target;
}

/// <summary>One agenda row, as the control's row templates bind to it.</summary>
public sealed class CalendarAgendaItem
{
    private static readonly HashSet<string> OnlinePlaces = new(StringComparer.OrdinalIgnoreCase) { "Teams", "Zoom", "Online", "Meet" };

    internal CalendarAgendaItem(CalendarEvent calendarEvent, ICommand optionsCommand)
    {
        Event = calendarEvent;
        OptionsCommand = optionsCommand;
        Time = calendarEvent.StartTime.ToString("h:mm", CalendarText.Culture);
        Meridiem = calendarEvent.StartTime.ToString("tt", CalendarText.Culture);
        Duration = CalendarText.Duration(calendarEvent.EndTime - calendarEvent.StartTime);
        IsOnline = OnlinePlaces.Contains(calendarEvent.Location);
    }

    public CalendarEvent Event { get; }

    public string Time { get; }

    public string Meridiem { get; }

    public string Duration { get; }

    public string Title => Event.Title;

    public string Detail => Event.Description;

    public string Location => Event.Location;

    public bool IsOnline { get; }

    public Visibility VideoIconVisibility => IsOnline ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PlaceIconVisibility => IsOnline ? Visibility.Collapsed : Visibility.Visible;

    public string OptionsName => $"More options for {Event.Title}";

    public ICommand OptionsCommand { get; }
}

internal sealed class CalendarCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}

internal static class CalendarText
{
    public static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

    public static string Duration(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours > 0 && rest > 0 ? $"{hours} h {rest} min" : hours > 0 ? $"{hours} h" : $"{rest} min";
    }

    public static string Events(int count) => count == 1 ? "1 event" : $"{count} events";
}
