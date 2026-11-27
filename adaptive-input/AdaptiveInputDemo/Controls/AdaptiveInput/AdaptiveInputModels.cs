using System.Windows.Input;
using AdaptiveInputDemo.Services;

namespace AdaptiveInputDemo.Controls;

/// <summary>Where the control is in its flow. Each value has a visual state in the InputStates group.</summary>
public enum AdaptiveInputState
{
    /// <summary>Nothing typed: the picker shows the date panel.</summary>
    Empty,
    /// <summary>The text reads as a date, colour, person or tag: a suggestion and the matching panel show.</summary>
    Recognised,
    /// <summary>Plain text (or Esc kept the text): "Kept as plain text".</summary>
    Plain,
    /// <summary>The text starts like a date but does not read as one: it stays text and the picker is offered.</summary>
    NotRecognised,
    /// <summary>A suggestion was accepted: the token sits in the field; Backspace in the empty field undoes it.</summary>
    Accepted,
}

/// <summary>Where suggestions show: a fixed-height panel under the field, or a bar above the keyboard on a phone.</summary>
public enum SuggestionsPlacement
{
    Panel,
    Accessory,
}

/// <summary>An accepted value.</summary>
public sealed record AdaptiveToken(DetectedInputType Type, string Label, string Value, DateTime? Date = null, TimeSpan? Time = null)
{
    /// <summary>Text shown in the token chip: "Date · Thu, Nov 26", "Colour · #FF5500", "@Ada Moreau", "#urgent".</summary>
    public string Display => Type switch
    {
        DetectedInputType.Date => "Date · " + Label,
        DetectedInputType.Color => "Colour · " + Label,
        DetectedInputType.Mention => "@" + Label,
        _ => Label,
    };
}

public sealed record AdaptivePerson(string Initials, string Name, string Handle);

public sealed record AdaptiveTag(string Name, string Note);

/// <summary>A row, quick pick, day or accessory button the template binds to.</summary>
public sealed class AdaptiveChoice
{
    public string Label { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public bool IsOn { get; init; }
    public bool IsAvailable { get; init; } = true;
    /// <summary>Today's ring in the calendar (1 or 0).</summary>
    public double RingOpacity { get; init; }
    /// <summary>Past days are dimmed.</summary>
    public double TextOpacity { get; init; } = 1;
    public string AutomationName { get; init; } = string.Empty;
    public ICommand? Pick { get; init; }
}

internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
