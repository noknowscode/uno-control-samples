using System.Collections.ObjectModel;

namespace RadialActionMenuDemo.Controls;

/// <summary>Tabler outline icons (24 px grid) for the demo's actions, and the demo's item set.</summary>
public static class RadialMenuIcons
{
    public const string Contact = "M8 7a4 4 0 1 0 8 0a4 4 0 0 0 -8 0 M6 21v-2a4 4 0 0 1 4 -4h4a4 4 0 0 1 4 4v2";
    public const string Email = "M3 7a2 2 0 0 1 2 -2h14a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-10z M3 7l9 6l9 -6";
    public const string Call = "M5 4h4l2 5l-2.5 1.5a11 11 0 0 0 5 5l1.5 -2.5l5 2v4a2 2 0 0 1 -2 2a16 16 0 0 1 -15 -15a2 2 0 0 1 2 -2";
    public const string Note = "M13 20l7 -7 M13 20v-6a1 1 0 0 1 1 -1h6v-7a2 2 0 0 0 -2 -2h-12a2 2 0 0 0 -2 2v12a2 2 0 0 0 2 2h7";
    public const string Search = "M3 10a7 7 0 1 0 14 0a7 7 0 1 0 -14 0 M21 21l-6 -6";

    /// <summary>Contact, Email, Call, Note, Search. <paramref name="callAvailable"/> false dims Call.</summary>
    public static ObservableCollection<RadialMenuItemData> ContactActions(bool callAvailable = true) => new()
    {
        new() { Icon = Contact, Label = "Contact" },
        new() { Icon = Email, Label = "Email" },
        new() { Icon = Call, Label = "Call", IsEnabled = callAvailable },
        new() { Icon = Note, Label = "Note" },
        new() { Icon = Search, Label = "Search" },
    };
}
