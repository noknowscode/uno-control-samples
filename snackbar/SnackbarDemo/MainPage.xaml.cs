using System.Collections.ObjectModel;
using Uno.Toolkit.UI;

namespace SnackbarDemo;

public sealed partial class MainPage : Page
{
    private readonly ObservableCollection<Draft> _drafts = new()
    {
        new("Quarterly letter to partners", "Nov 24"),
        new("Sauce day shopping list", "Nov 21"),
        new("Notes from the roadmap review", "Nov 18"),
        new("Venue shortlist for the offsite", "Nov 12"),
        new("Replies for the hockey league", "Nov 9"),
    };

    public MainPage()
    {
        this.InitializeComponent();
        DraftList.ItemsSource = _drafts;
        _drafts.CollectionChanged += (_, _) => UpdateCount();
        UpdateCount();
        Loaded += (_, _) => ThemeToggle.IsChecked = SystemThemeHelper.IsRootInDarkMode(ThemeRoot);
    }

    // App.AppRoot is null when a host (Studio, the Gallery) creates the page without App.OnLaunched.
    private FrameworkElement ThemeRoot => App.AppRoot ?? this;

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        => SystemThemeHelper.SetRootTheme(ThemeRoot, ThemeToggle.IsChecked == true);

    private void UpdateCount() => CountText.Text = _drafts.Count == 1 ? "1 draft" : $"{_drafts.Count} drafts";

    // Undo: 7 s, one action. Undo puts the draft back where it was and confirms.
    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Draft draft) return;
        var index = _drafts.IndexOf(draft);
        if (index < 0) return;
        _drafts.RemoveAt(index);

        var reason = await SnackbarHost.ShowAsync(new SnackbarItem
        {
            Kind = SnackbarKind.Undo,
            Message = "Draft deleted",
            ActionLabel = "Undo",
        });

        if (reason == SnackbarDismissReason.Action)
        {
            _drafts.Insert(Math.Min(index, _drafts.Count), draft);
            _ = SnackbarHost.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Confirm, Message = "Draft restored" });
        }
    }

    // Confirm: a quiet success, 4 s, no action.
    private void OnSaveClick(object sender, RoutedEventArgs e)
        => _ = SnackbarHost.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Confirm, Message = "All changes saved" });

    // Attention: stays until dismissed; Retry finishes the export.
    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var reason = await SnackbarHost.ShowAsync(new SnackbarItem
        {
            Kind = SnackbarKind.Attention,
            Message = "Export didn’t finish. The file is still on this device.",
            ActionLabel = "Retry",
        });

        if (reason == SnackbarDismissReason.Action)
        {
            _ = SnackbarHost.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Confirm, Message = "Export complete · drafts.zip" });
        }
    }

    // x:Bind function: 96 px clears a one-line message; a taller one (the Attention message with its Retry row)
    // pushes the add button up to 16 px above it.
    public Thickness AddButtonMargin(double occupiedHeight) => new(0, 0, 20, Math.Max(96, occupiedHeight + 16));

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        _drafts.Insert(0, new Draft("Untitled draft", DateTime.Now.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)));
        _ = SnackbarHost.ShowAsync(new SnackbarItem { Kind = SnackbarKind.Confirm, Message = "Draft added" });
    }
}

public sealed record Draft(string Title, string Date)
{
    public string DeleteLabel => "Delete " + Title;
}
