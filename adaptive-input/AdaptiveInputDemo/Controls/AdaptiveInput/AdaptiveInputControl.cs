using System.Globalization;
using AdaptiveInputDemo.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.System;

namespace AdaptiveInputDemo.Controls;

/// <summary>
/// A text field that reads what you type as a date, a #colour, an @person or a #tag, suggests the value (Tab or click
/// accepts, Esc keeps the text) and shows a matching picker anchored under the field at a fixed height. An accepted
/// value becomes a token; Backspace in the empty field turns it back into the text. On a phone the suggestions ride in
/// a bar above the keyboard (<see cref="SuggestionsPlacement.Accessory"/>).
/// </summary>
[TemplatePart(Name = TextBoxPart, Type = typeof(TextBox))]
[TemplatePart(Name = TokenPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = SuggestionPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ClearPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = SubmitPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = UseColourPart, Type = typeof(ButtonBase))]
[TemplatePart(Name = ChooseDatePart, Type = typeof(ButtonBase))]
public sealed partial class AdaptiveInputControl : Control
{
    private const string TextBoxPart = "PART_TextBox";
    private const string TokenPart = "PART_Token";
    private const string SuggestionPart = "PART_Suggestion";
    private const string ClearPart = "PART_Clear";
    private const string SubmitPart = "PART_Submit";
    private const string UseColourPart = "PART_UseColour";
    private const string ChooseDatePart = "PART_ChooseDate";

    private TextBox? _textBox;
    private FrameworkElement? _token;
    private ButtonBase? _suggestion, _clear, _submit, _useColour, _chooseDate;
    private bool _isReady;
    private bool _dismissed;
    private bool _forceDatePanel;
    private string _previousText = string.Empty;
    private int _activeIndex;
    private InputDetectionResult? _result;

    /// <summary>Raised when a suggestion is accepted (Tab, Enter on a row, or a click).</summary>
    public event EventHandler<AdaptiveToken>? TokenAccepted;

    /// <summary>Raised by the add button and by Enter when nothing is suggested.</summary>
    public event EventHandler? Submitted;

    public AdaptiveInputControl()
    {
        DefaultStyleKey = typeof(AdaptiveInputControl);
        People =
        [
            new("AM", "Ada Moreau", "ada"), new("AD", "Adrien Dubé", "adrien"), new("AL", "Alix Laporte", "alix"),
            new("JP", "Jonas Petit", "jonas"), new("LR", "Léa Roy", "lea"),
        ];
        Tags = [new("urgent", "12 tasks"), new("uno-7", "8 tasks"), new("gallery", "5 tasks"), new("sauce-day", "2 tasks")];
    }

    #region Dependency properties

    private static PropertyMetadata Recompute(object defaultValue)
        => new(defaultValue, (s, e) => ((AdaptiveInputControl)s).Update());

    public static DependencyProperty ValueProperty { get; } = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(AdaptiveInputControl),
        new PropertyMetadata(string.Empty, (s, e) => ((AdaptiveInputControl)s).OnValueChanged((string?)e.NewValue)));

    /// <summary>The text in the field.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static DependencyProperty PlaceholderProperty { get; } = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(AdaptiveInputControl),
        new PropertyMetadata("Type a date, #colour, @person or #tag"));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static DependencyProperty HeaderProperty { get; } = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(AdaptiveInputControl), new PropertyMetadata(string.Empty));

    /// <summary>The field's label ("Task details"); hidden when empty.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static DependencyProperty ReferenceDateProperty { get; } = DependencyProperty.Register(
        nameof(ReferenceDate), typeof(DateTime?), typeof(AdaptiveInputControl), Recompute(null!));

    /// <summary>"Today" for reading relative dates. Null uses the current date.</summary>
    public DateTime? ReferenceDate
    {
        get => (DateTime?)GetValue(ReferenceDateProperty);
        set => SetValue(ReferenceDateProperty, value);
    }

    public static DependencyProperty PeopleProperty { get; } = DependencyProperty.Register(
        nameof(People), typeof(IList<AdaptivePerson>), typeof(AdaptiveInputControl), Recompute(null!));

    /// <summary>People offered for @mentions (matched on handle prefix or name).</summary>
    public IList<AdaptivePerson> People
    {
        get => (IList<AdaptivePerson>)GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    public static DependencyProperty TagsProperty { get; } = DependencyProperty.Register(
        nameof(Tags), typeof(IList<AdaptiveTag>), typeof(AdaptiveInputControl), Recompute(null!));

    /// <summary>Tags offered for #tags (matched on prefix; an unknown tag is offered as "New tag").</summary>
    public IList<AdaptiveTag> Tags
    {
        get => (IList<AdaptiveTag>)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public static DependencyProperty SuggestionsPlacementProperty { get; } = DependencyProperty.Register(
        nameof(SuggestionsPlacement), typeof(SuggestionsPlacement), typeof(AdaptiveInputControl), Recompute(SuggestionsPlacement.Panel));

    /// <summary>Panel: the picker under the field (300 px). Accessory: a bar of up to three suggestions at the bottom.</summary>
    public SuggestionsPlacement SuggestionsPlacement
    {
        get => (SuggestionsPlacement)GetValue(SuggestionsPlacementProperty);
        set => SetValue(SuggestionsPlacementProperty, value);
    }

    public static DependencyProperty IsPickerOpenProperty { get; } = DependencyProperty.Register(
        nameof(IsPickerOpen), typeof(bool), typeof(AdaptiveInputControl), Recompute(false));

    /// <summary>In the accessory placement, opens the full picker above the bar ("Pick…").</summary>
    public bool IsPickerOpen
    {
        get => (bool)GetValue(IsPickerOpenProperty);
        set => SetValue(IsPickerOpenProperty, value);
    }

    public static DependencyProperty StateProperty { get; } = DependencyProperty.Register(
        nameof(State), typeof(AdaptiveInputState), typeof(AdaptiveInputControl), new PropertyMetadata(AdaptiveInputState.Empty));

    /// <summary>Empty, Recognised, Plain, NotRecognised or Accepted.</summary>
    public AdaptiveInputState State
    {
        get => (AdaptiveInputState)GetValue(StateProperty);
        private set => SetValue(StateProperty, value);
    }

    public static DependencyProperty DetectedTypeProperty { get; } = DependencyProperty.Register(
        nameof(DetectedType), typeof(DetectedInputType), typeof(AdaptiveInputControl), new PropertyMetadata(DetectedInputType.None));

    public DetectedInputType DetectedType
    {
        get => (DetectedInputType)GetValue(DetectedTypeProperty);
        private set => SetValue(DetectedTypeProperty, value);
    }

    public static DependencyProperty AcceptedTokenProperty { get; } = DependencyProperty.Register(
        nameof(AcceptedToken), typeof(AdaptiveToken), typeof(AdaptiveInputControl), new PropertyMetadata(null));

    public AdaptiveToken? AcceptedToken
    {
        get => (AdaptiveToken?)GetValue(AcceptedTokenProperty);
        private set => SetValue(AcceptedTokenProperty, value);
    }

    // Values the template shows. Set by the control.
    public static DependencyProperty SuggestionTextProperty { get; } = Shown(nameof(SuggestionText), string.Empty);
    public string SuggestionText { get => (string)GetValue(SuggestionTextProperty); private set => SetValue(SuggestionTextProperty, value); }

    public static DependencyProperty StatusTextProperty { get; } = Shown(nameof(StatusText), string.Empty);
    public string StatusText { get => (string)GetValue(StatusTextProperty); private set => SetValue(StatusTextProperty, value); }

    public static DependencyProperty TokenTextProperty { get; } = Shown(nameof(TokenText), string.Empty);
    public string TokenText { get => (string)GetValue(TokenTextProperty); private set => SetValue(TokenTextProperty, value); }

    public static DependencyProperty AcceptedTitleProperty { get; } = Shown(nameof(AcceptedTitle), string.Empty);
    public string AcceptedTitle { get => (string)GetValue(AcceptedTitleProperty); private set => SetValue(AcceptedTitleProperty, value); }

    public static DependencyProperty MonthTitleProperty { get; } = Shown(nameof(MonthTitle), string.Empty);
    public string MonthTitle { get => (string)GetValue(MonthTitleProperty); private set => SetValue(MonthTitleProperty, value); }

    public static DependencyProperty TimeNoteProperty { get; } = Shown(nameof(TimeNote), string.Empty);
    public string TimeNote { get => (string)GetValue(TimeNoteProperty); private set => SetValue(TimeNoteProperty, value); }

    public static DependencyProperty HexTextProperty { get; } = Shown(nameof(HexText), string.Empty);
    public string HexText { get => (string)GetValue(HexTextProperty); private set => SetValue(HexTextProperty, value); }

    public static DependencyProperty RgbTextProperty { get; } = Shown(nameof(RgbText), string.Empty);
    public string RgbText { get => (string)GetValue(RgbTextProperty); private set => SetValue(RgbTextProperty, value); }

    public static DependencyProperty NoMatchTextProperty { get; } = Shown(nameof(NoMatchText), string.Empty);
    public string NoMatchText { get => (string)GetValue(NoMatchTextProperty); private set => SetValue(NoMatchTextProperty, value); }

    public static DependencyProperty SwatchBrushProperty { get; } = Shown(nameof(SwatchBrush), null);
    public Brush? SwatchBrush { get => (Brush?)GetValue(SwatchBrushProperty); private set => SetValue(SwatchBrushProperty, value); }

    public static DependencyProperty DaysProperty { get; } = Shown(nameof(Days), null);
    public IReadOnlyList<AdaptiveChoice>? Days { get => (IReadOnlyList<AdaptiveChoice>?)GetValue(DaysProperty); private set => SetValue(DaysProperty, value); }

    public static DependencyProperty QuickPicksProperty { get; } = Shown(nameof(QuickPicks), null);
    public IReadOnlyList<AdaptiveChoice>? QuickPicks { get => (IReadOnlyList<AdaptiveChoice>?)GetValue(QuickPicksProperty); private set => SetValue(QuickPicksProperty, value); }

    public static DependencyProperty RowsProperty { get; } = Shown(nameof(Rows), null);
    /// <summary>The people or tag rows for the current text.</summary>
    public IReadOnlyList<AdaptiveChoice>? Rows { get => (IReadOnlyList<AdaptiveChoice>?)GetValue(RowsProperty); private set => SetValue(RowsProperty, value); }

    public static DependencyProperty AccessoryItemsProperty { get; } = Shown(nameof(AccessoryItems), null);
    public IReadOnlyList<AdaptiveChoice>? AccessoryItems { get => (IReadOnlyList<AdaptiveChoice>?)GetValue(AccessoryItemsProperty); private set => SetValue(AccessoryItemsProperty, value); }

    private static DependencyProperty Shown(string name, object? defaultValue)
        => DependencyProperty.Register(name, typeof(object), typeof(AdaptiveInputControl), new PropertyMetadata(defaultValue));

    #endregion

    #region Public methods

    /// <summary>Accepts the current suggestion (the active row for people and tags).</summary>
    public void Accept()
    {
        if (_result is not { } r) return;
        AcceptToken(r.Type switch
        {
            DetectedInputType.Date => new AdaptiveToken(DetectedInputType.Date, InputTypeDetector.FormatDate(r.Date!.Value, r.Time), r.Value, r.Date, r.Time),
            DetectedInputType.Color => new AdaptiveToken(DetectedInputType.Color, InputTypeDetector.ExpandHex(r.Value), InputTypeDetector.ExpandHex(r.Value)),
            DetectedInputType.Mention => PersonToken(MatchPeople(r.Value).ElementAtOrDefault(_activeIndex) ?? People.FirstOrDefault()),
            _ => TagToken(MatchTags(r.Value).ElementAtOrDefault(_activeIndex)?.Name ?? r.Value),
        });
    }

    /// <summary>Esc: keeps the text as typed and drops the suggestion.</summary>
    public void Dismiss()
    {
        _dismissed = true;
        Update();
    }

    /// <summary>Backspace in the empty field: the token turns back into the text that produced it.</summary>
    public void Undo()
    {
        if (AcceptedToken == null) return;
        AcceptedToken = null;
        SetText(_previousText);
    }

    /// <summary>Clears the text, the token and Esc.</summary>
    public void Clear()
    {
        AcceptedToken = null;
        _dismissed = false;
        _forceDatePanel = false;
        SetText(string.Empty);
    }

    #endregion

    protected override void OnApplyTemplate()
    {
        if (_textBox != null)
        {
            _textBox.TextChanged -= OnTextChanged;
            _textBox.GotFocus -= OnFieldFocus;
            _textBox.LostFocus -= OnFieldFocus;
            _textBox.PreviewKeyDown -= OnFieldKeyDown;
        }
        Unwire(_suggestion, OnSuggestionClick);
        Unwire(_useColour, OnSuggestionClick);
        Unwire(_clear, OnClearClick);
        Unwire(_submit, OnSubmitClick);
        Unwire(_chooseDate, OnChooseDateClick);

        base.OnApplyTemplate();

        _textBox = GetTemplateChild(TextBoxPart) as TextBox;
        _token = GetTemplateChild(TokenPart) as FrameworkElement;
        _suggestion = GetTemplateChild(SuggestionPart) as ButtonBase;
        _clear = GetTemplateChild(ClearPart) as ButtonBase;
        _submit = GetTemplateChild(SubmitPart) as ButtonBase;
        _useColour = GetTemplateChild(UseColourPart) as ButtonBase;
        _chooseDate = GetTemplateChild(ChooseDatePart) as ButtonBase;

        if (_textBox != null)
        {
            _textBox.Text = Value ?? string.Empty;
            _textBox.TextChanged += OnTextChanged;
            _textBox.GotFocus += OnFieldFocus;
            _textBox.LostFocus += OnFieldFocus;
            // Preview (tunnelling): the keys are taken before the TextBox acts on them (Tab would move focus,
            // Backspace would delete a character of the restored text).
            _textBox.PreviewKeyDown += OnFieldKeyDown;
        }
        Wire(_suggestion, OnSuggestionClick);
        Wire(_useColour, OnSuggestionClick);
        Wire(_clear, OnClearClick);
        Wire(_submit, OnSubmitClick);
        Wire(_chooseDate, OnChooseDateClick);

        _isReady = true;
        Update(useTransitions: false);
        UpdateFieldState(false);
    }

    private static void Wire(ButtonBase? b, RoutedEventHandler h) { if (b != null) b.Click += h; }
    private static void Unwire(ButtonBase? b, RoutedEventHandler h) { if (b != null) b.Click -= h; }

    /// <summary>Moves keyboard focus into the field.</summary>
    public void FocusField() => _textBox?.Focus(FocusState.Programmatic);

    #region Input

    private void OnValueChanged(string? value)
    {
        if (_textBox != null && _textBox.Text != (value ?? string.Empty)) _textBox.Text = value ?? string.Empty;
        _dismissed = false;
        _forceDatePanel = false;
        _activeIndex = 0;
        Update();
    }

    private void SetText(string text)
    {
        Value = text;
        if (_textBox != null)
        {
            _textBox.Text = text;
            _textBox.SelectionStart = text.Length;
        }
        Update();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_textBox != null && _textBox.Text != Value) Value = _textBox.Text;
    }

    private void OnFieldFocus(object sender, RoutedEventArgs e) => UpdateFieldState(true);

    private void UpdateFieldState(bool useTransitions)
    {
        var focused = _textBox != null && _textBox.FocusState != FocusState.Unfocused;
        VisualStateManager.GoToState(this, focused ? "FieldFocused" : "FieldNormal", useTransitions);
    }

    private void OnFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Tab when _result != null && AcceptedToken == null:
                Accept();
                e.Handled = true;
                break;
            case VirtualKey.Escape when _result != null:
                Dismiss();
                e.Handled = true;
                break;
            case VirtualKey.Back when AcceptedToken != null && string.IsNullOrEmpty(_textBox?.Text):
                // After the TextBox has seen the key: on Skia it still applies Backspace to the restored text.
                e.Handled = true;
                DispatcherQueue.TryEnqueue(Undo);
                break;
            case VirtualKey.Enter:
                if (_result is { Type: DetectedInputType.Mention or DetectedInputType.Tag }) Accept();
                else Submitted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case VirtualKey.Down or VirtualKey.Up when _result is { Type: DetectedInputType.Mention or DetectedInputType.Tag }:
                var count = Rows?.Count ?? 0;
                if (count > 0)
                {
                    _activeIndex = (_activeIndex + (e.Key == VirtualKey.Down ? 1 : -1) + count) % count;
                    Update();
                }
                e.Handled = true;
                break;
        }
    }

    private void OnSuggestionClick(object sender, RoutedEventArgs e) { Accept(); FocusField(); }
    private void OnClearClick(object sender, RoutedEventArgs e) { Clear(); FocusField(); }
    private void OnSubmitClick(object sender, RoutedEventArgs e) => Submitted?.Invoke(this, EventArgs.Empty);
    private void OnChooseDateClick(object sender, RoutedEventArgs e) { _forceDatePanel = true; IsPickerOpen = true; Update(); }

    private void AcceptToken(AdaptiveToken token)
    {
        _previousText = Value ?? string.Empty;
        AcceptedToken = token;
        _forceDatePanel = false;
        IsPickerOpen = false;
        SetText(string.Empty);
        PlayTokenIn();
        TokenAccepted?.Invoke(this, token);
    }

    private static AdaptiveToken TagToken(string name) => new(DetectedInputType.Tag, "#" + name, name);

    private static AdaptiveToken PersonToken(AdaptivePerson? p)
        => new(DetectedInputType.Mention, p?.Name ?? string.Empty, p?.Handle ?? string.Empty);

    private AdaptiveToken DateToken(DateTime d) => new(DetectedInputType.Date, InputTypeDetector.FormatDate(d), d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d);

    // Token enters: 260 ms, from 88% scale and 24 px to the right (cubic-bezier(.2,.9,.3,1)).
    private void PlayTokenIn()
    {
        if (_token == null || !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var t = new CompositeTransform();
        _token.RenderTransform = t;
        _token.RenderTransformOrigin = new Point(0, 0.5);
        var spline = new KeySpline { ControlPoint1 = new Point(0.2, 0.9), ControlPoint2 = new Point(0.3, 1) };
        var sb = new Storyboard();
        Add(t, nameof(CompositeTransform.ScaleX), 0.88, 1);
        Add(t, nameof(CompositeTransform.ScaleY), 0.88, 1);
        Add(t, nameof(CompositeTransform.TranslateX), 24, 0);
        Add(_token, "Opacity", 0, 1);
        sb.Completed += (_, _) => { sb.Stop(); if (_token != null) _token.RenderTransform = null; };
        sb.Begin();

        void Add(DependencyObject target, string property, double from, double to)
        {
            var a = new DoubleAnimationUsingKeyFrames();
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = from });
            a.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), Value = to, KeySpline = spline });
            Storyboard.SetTarget(a, target);
            Storyboard.SetTargetProperty(a, property);
            sb.Children.Add(a);
        }
    }

    #endregion

    #region State

    private DateTime Today => (ReferenceDate ?? DateTime.Today).Date;

    private IEnumerable<AdaptivePerson> MatchPeople(string query)
    {
        var q = query.ToLowerInvariant();
        return (People ?? []).Where(p => p.Handle.StartsWith(q, StringComparison.Ordinal) || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(4);
    }

    private IEnumerable<AdaptiveTag> MatchTags(string query)
    {
        var q = query.ToLowerInvariant();
        var matches = (Tags ?? []).Where(t => t.Name.StartsWith(q, StringComparison.Ordinal)).ToList();
        return matches.Count > 0 ? matches : [new AdaptiveTag(q, "New tag")];
    }

    private static string Kind(DetectedInputType t) => t switch
    {
        DetectedInputType.Date => "a date",
        DetectedInputType.Color => "a colour",
        DetectedInputType.Mention => "a person",
        _ => "a tag",
    };

    private void Update() => Update(useTransitions: true);

    private void Update(bool useTransitions)
    {
        var text = Value ?? string.Empty;
        var token = AcceptedToken;
        _result = token != null || _dismissed ? null : InputTypeDetector.Detect(text, Today);
        var r = _result;

        State = token != null ? AdaptiveInputState.Accepted
            : r != null ? AdaptiveInputState.Recognised
            : string.IsNullOrWhiteSpace(text) ? AdaptiveInputState.Empty
            : !_dismissed && InputTypeDetector.LooksLikeDate(text) ? AdaptiveInputState.NotRecognised
            : AdaptiveInputState.Plain;
        DetectedType = r?.Type ?? DetectedInputType.None;

        StatusText = State switch
        {
            AdaptiveInputState.Accepted => $"Read as {Kind(token!.Type)}. Backspace to undo.",
            AdaptiveInputState.Recognised => $"Looks like {Kind(r!.Type)}. Tab accepts, Esc keeps text.",
            AdaptiveInputState.NotRecognised => "Doesn’t read as a date. It stays as text.",
            AdaptiveInputState.Plain => "Plain text",
            _ => string.Empty,
        };
        TokenText = token?.Display ?? string.Empty;
        AcceptedTitle = token == null ? string.Empty : token.Type switch
        {
            DetectedInputType.Date => "Due ",
            DetectedInputType.Color => "Colour ",
            DetectedInputType.Mention => "Assigned to ",
            _ => "Tagged ",
        } + token.Label;
        SuggestionText = r?.Type switch
        {
            DetectedInputType.Date => InputTypeDetector.FormatDate(r.Date!.Value, r.Time),
            DetectedInputType.Color => InputTypeDetector.ExpandHex(r.Value),
            _ => string.Empty,
        };

        BuildDatePanel(r);
        BuildColour(r, token);
        BuildRows(r);
        BuildAccessory(r);

        if (!_isReady) return;

        var panel = State switch
        {
            AdaptiveInputState.Accepted => "AcceptedPanel",
            AdaptiveInputState.Recognised => r!.Type switch
            {
                DetectedInputType.Color => "ColourPanel",
                DetectedInputType.Mention => "PeoplePanel",
                DetectedInputType.Tag => "TagsPanel",
                _ => "DatePanel",
            },
            AdaptiveInputState.NotRecognised => _forceDatePanel ? "DatePanel" : "NotRecognisedPanel",
            AdaptiveInputState.Plain => "PlainPanel",
            _ => "DatePanel",
        };

        Go(State.ToString());
        Go(panel);
        Go(SuggestionText.Length > 0 ? "Suggestion" : "NoSuggestion");
        Go(token == null ? "NoToken" : token.Type == DetectedInputType.Color ? "ColourToken" : "Token");
        Go(SuggestionsPlacement == SuggestionsPlacement.Panel ? "PanelPlacement" : IsPickerOpen ? "AccessoryPickerOpen" : "AccessoryPlacement");
        Go(string.IsNullOrEmpty(NoMatchText) ? "Matches" : "NoMatches");
        Go(r?.Type == DetectedInputType.Color ? "ColourSuggestion" : "TextSuggestion");

        void Go(string s) => VisualStateManager.GoToState(this, s, useTransitions);
    }

    private void BuildDatePanel(InputDetectionResult? r)
    {
        var today = Today;
        var selected = r?.Type == DetectedInputType.Date ? r.Date!.Value : today.AddDays(1);
        var first = new DateTime(selected.Year, selected.Month, 1);
        MonthTitle = first.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        TimeNote = r?.Time is { } t ? "at " + today.Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture) : string.Empty;

        var offset = (int)first.DayOfWeek;
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        var list = new List<AdaptiveChoice>(42);
        for (var i = 0; i < 42; i++)
        {
            var n = i - offset + 1;
            if (n < 1 || n > days)
            {
                list.Add(new AdaptiveChoice { IsAvailable = false });
                continue;
            }
            var d = first.AddDays(n - 1);
            list.Add(new AdaptiveChoice
            {
                Label = n.ToString(CultureInfo.InvariantCulture),
                IsOn = d == selected,
                RingOpacity = d == today ? 1 : 0,
                TextOpacity = d < today ? 0.55 : 1,
                AutomationName = d.ToString("MMMM d", CultureInfo.InvariantCulture),
                Pick = new RelayCommand(() => AcceptToken(DateToken(d))),
            });
        }
        Days = list;

        (string Label, DateTime Date)[] quick =
        [
            ("Today", today),
            ("Tomorrow", today.AddDays(1)),
            ("Next Monday", today.AddDays((8 - (int)today.DayOfWeek) % 7 is 0 ? 7 : (8 - (int)today.DayOfWeek) % 7)),
            ("In a week", today.AddDays(7)),
        ];
        QuickPicks = quick.Select(q => new AdaptiveChoice
        {
            Label = q.Label,
            IsOn = q.Date == selected,
            Pick = new RelayCommand(() => AcceptToken(DateToken(q.Date))),
        }).ToList();
    }

    private void BuildColour(InputDetectionResult? r, AdaptiveToken? token)
    {
        var hex = r?.Type == DetectedInputType.Color ? r.Value : token?.Type == DetectedInputType.Color ? token.Value : null;
        if (hex == null) return;
        var c = InputTypeDetector.ToColor(hex);
        SwatchBrush = new SolidColorBrush(c);
        HexText = InputTypeDetector.ExpandHex(hex);
        RgbText = $"{c.R} {c.G} {c.B}";
    }

    private void BuildRows(InputDetectionResult? r)
    {
        NoMatchText = string.Empty;
        if (r?.Type == DetectedInputType.Mention)
        {
            var people = MatchPeople(r.Value).ToList();
            if (_activeIndex >= people.Count) _activeIndex = 0;
            Rows = people.Select((p, i) => new AdaptiveChoice
            {
                Label = p.Name,
                Detail = "@" + p.Handle,
                Initials = p.Initials,
                IsOn = i == _activeIndex,
                AutomationName = p.Name,
                Pick = new RelayCommand(() => AcceptToken(PersonToken(p))),
            }).ToList();
            if (people.Count == 0) NoMatchText = $"No one matches “{r.Value}”.";
        }
        else if (r?.Type == DetectedInputType.Tag)
        {
            var tags = MatchTags(r.Value).ToList();
            if (_activeIndex >= tags.Count) _activeIndex = 0;
            Rows = tags.Select((t, i) => new AdaptiveChoice
            {
                Label = t.Name,
                Detail = t.Note,
                IsOn = i == _activeIndex,
                AutomationName = "#" + t.Name,
                Pick = new RelayCommand(() => AcceptToken(TagToken(t.Name))),
            }).ToList();
        }
        else
        {
            Rows = [];
        }
    }

    // The phone bar: up to three suggestions for what is typed, plus "Pick…" for dates.
    private void BuildAccessory(InputDetectionResult? r)
    {
        var items = new List<AdaptiveChoice>();
        if (AcceptedToken != null)
        {
            AccessoryItems = items;
            return;
        }

        switch (r?.Type)
        {
            case DetectedInputType.Color:
                items.Add(new AdaptiveChoice { Label = "Use " + InputTypeDetector.ExpandHex(r.Value), IsOn = true, Pick = new RelayCommand(Accept) });
                break;
            case DetectedInputType.Mention or DetectedInputType.Tag:
                items.AddRange((Rows ?? []).Take(3).Select(row => new AdaptiveChoice
                {
                    Label = r.Type == DetectedInputType.Tag ? "#" + row.Label : row.Label,
                    IsOn = row.IsOn,
                    Pick = row.Pick,
                }));
                break;
            default:
                var d = r?.Type == DetectedInputType.Date ? r.Date!.Value : Today.AddDays(1);
                items.Add(new AdaptiveChoice { Label = InputTypeDetector.FormatDate(d), IsOn = r != null, Pick = new RelayCommand(() => AcceptToken(DateToken(d))) });
                items.Add(new AdaptiveChoice { Label = InputTypeDetector.FormatDate(d.AddDays(1)), Pick = new RelayCommand(() => AcceptToken(DateToken(d.AddDays(1)))) });
                items.Add(new AdaptiveChoice { Label = "Pick…", Initials = "calendar", Pick = new RelayCommand(() => { _forceDatePanel = true; IsPickerOpen = !IsPickerOpen; }) });
                break;
        }
        AccessoryItems = items;
    }

    #endregion
}
