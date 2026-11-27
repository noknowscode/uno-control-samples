using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace Uno.Toolkit.UI;

[TemplatePart(Name = PART_ItemsPanel, Type = typeof(CarouselPanel))]
[TemplatePart(Name = PART_IndicatorPanel, Type = typeof(CarouselIndicator))]
[TemplatePart(Name = PART_ThumbnailPanel, Type = typeof(Panel))]
[TemplatePart(Name = PART_PreviousButton, Type = typeof(ButtonBase))]
[TemplatePart(Name = PART_NextButton, Type = typeof(ButtonBase))]
[TemplatePart(Name = PART_PlayPauseButton, Type = typeof(ButtonBase))]
[TemplatePart(Name = PART_TransportRow, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PART_StatusText, Type = typeof(TextBlock))]
[TemplatePart(Name = PART_PositionText, Type = typeof(TextBlock))]
[TemplatePart(Name = PART_CaptionPresenterA, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PART_CaptionPresenterB, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PART_ClipGrid, Type = typeof(Grid))]
[TemplateVisualState(GroupName = "PlacementStates", Name = "Side")]
[TemplateVisualState(GroupName = "PlacementStates", Name = "Bottom")]
[TemplateVisualState(GroupName = "PlaybackStates", Name = "Off")]
[TemplateVisualState(GroupName = "PlaybackStates", Name = "Playing")]
[TemplateVisualState(GroupName = "PlaybackStates", Name = "Held")]
[TemplateVisualState(GroupName = "PlaybackStates", Name = "Paused")]
public partial class Carousel : Control
{
    private const string PART_ItemsPanel = "PART_ItemsPanel";
    private const string PART_IndicatorPanel = "PART_IndicatorPanel";
    private const string PART_ThumbnailPanel = "PART_ThumbnailPanel";
    private const string PART_PreviousButton = "PART_PreviousButton";
    private const string PART_NextButton = "PART_NextButton";
    private const string PART_PlayPauseButton = "PART_PlayPauseButton";
    private const string PART_TransportRow = "PART_TransportRow";
    private const string PART_StatusText = "PART_StatusText";
    private const string PART_PositionText = "PART_PositionText";
    private const string PART_CaptionPresenterA = "PART_CaptionPresenterA";
    private const string PART_CaptionPresenterB = "PART_CaptionPresenterB";
    private const string PART_ClipGrid = "PART_ClipGrid";

    // Track glide: 700 ms, cubic-bezier(.22, 1, .36, 1), settles without overshoot.
    private static readonly TimeSpan GlideDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SettleDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan CaptionDelay = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan CaptionFade = TimeSpan.FromMilliseconds(360);
    private static readonly TimeSpan CaptionRise = TimeSpan.FromMilliseconds(480);
    private const double SwipeThreshold = 40;

    private static KeySpline Glide() => new() { ControlPoint1 = new(0.22, 1), ControlPoint2 = new(0.36, 1) };
    private static KeySpline Smooth() => new() { ControlPoint1 = new(0.23, 1), ControlPoint2 = new(0.32, 1) };
    private static KeySpline Hover() => new() { ControlPoint1 = new(0.25, 0.1), ControlPoint2 = new(0.25, 1) };

    private CarouselPanel? _itemsPanel;
    private CarouselIndicator? _indicatorPanel;
    private Panel? _thumbnailPanel;
    private ButtonBase? _previousButton;
    private ButtonBase? _nextButton;
    private ButtonBase? _playPauseButton;
    private FrameworkElement? _transportRow;
    private TextBlock? _statusText;
    private TextBlock? _positionText;
    private ContentPresenter? _captionA;
    private ContentPresenter? _captionB;
    private bool _captionAIsActive = true;
    private Storyboard? _captionStoryboard;
    private Grid? _clipGrid;

    internal ICarouselTransition _transition = new SlideTransition();
    private DispatcherTimer? _autoPlayTimer;
    private TimeSpan _remaining;
    private long _segmentStart;
    private bool _timerRunning;
    private Storyboard? _fillStoryboard;
    private bool _pointerOver;
    private bool _focusWithin;
    private bool _isBottomPlacement;

    private bool _isSynchronizingSelection;
    private bool _isTransitioning;
    private bool _isManipulating;
    private int _selectedIndex = -1;
    private int _itemCount;
    private IList? _itemsSource;
    private INotifyCollectionChanged? _observedCollection;
    private readonly List<CarouselThumbnail> _thumbnails = [];

    private readonly TranslateTransform _trackTransform = new();
    private Storyboard? _trackStoryboard;
    private Storyboard? _opacityStoryboard;
    private double _manipulationBase;

    private UIElement? _peekElement;
    private int _peekIndex = -1;

    public Carousel()
    {
        DefaultStyleKey = typeof(Carousel);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdatePlacement(true);
        // The position label's emphasised run takes a brush in code; rebuild it once the theme's brushes are in.
        ActualThemeChanged += (_, _) => DispatcherQueue?.TryEnqueue(UpdatePositionText);
    }

    internal int ItemCount => _itemCount;

    private bool IsTrack => _itemsPanel?.IsTrack == true;

    private bool IsHeld => IsPaused || _pointerOver || _focusWithin;

    private bool IsAutoPlayActive => IsAutoPlayEnabled && IsLoaded && _itemCount > 1
        && AutoPlayInterval > TimeSpan.Zero;

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselAutomationPeer(this);

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_previousButton != null) _previousButton.Click -= OnNavButtonClick;
        if (_nextButton != null) _nextButton.Click -= OnNavButtonClick;
        if (_playPauseButton != null) _playPauseButton.Click -= OnPlayPauseClick;
        if (_itemsPanel != null) _itemsPanel.SizeChanged -= OnItemsPanelSizeChanged;
        if (_clipGrid != null)
        {
            _clipGrid.SizeChanged -= OnClipGridSizeChanged;
            _clipGrid.ManipulationStarted -= OnManipulationStarted;
            _clipGrid.ManipulationDelta -= OnManipulationDelta;
            _clipGrid.ManipulationCompleted -= OnManipulationCompleted;
        }

        _clipGrid = GetTemplateChild(PART_ClipGrid) as Grid;
        _itemsPanel = GetTemplateChild(PART_ItemsPanel) as CarouselPanel;
        _indicatorPanel = GetTemplateChild(PART_IndicatorPanel) as CarouselIndicator;
        _thumbnailPanel = GetTemplateChild(PART_ThumbnailPanel) as Panel;
        _previousButton = GetTemplateChild(PART_PreviousButton) as ButtonBase;
        _nextButton = GetTemplateChild(PART_NextButton) as ButtonBase;
        _playPauseButton = GetTemplateChild(PART_PlayPauseButton) as ButtonBase;
        _transportRow = GetTemplateChild(PART_TransportRow) as FrameworkElement;
        _statusText = GetTemplateChild(PART_StatusText) as TextBlock;
        _positionText = GetTemplateChild(PART_PositionText) as TextBlock;
        _captionA = GetTemplateChild(PART_CaptionPresenterA) as ContentPresenter;
        _captionB = GetTemplateChild(PART_CaptionPresenterB) as ContentPresenter;

        if (_previousButton != null) _previousButton.Click += OnNavButtonClick;
        if (_nextButton != null) _nextButton.Click += OnNavButtonClick;
        if (_playPauseButton != null) _playPauseButton.Click += OnPlayPauseClick;
        if (_itemsPanel != null)
        {
            _itemsPanel.RenderTransform = _trackTransform;
            _itemsPanel.SizeChanged += OnItemsPanelSizeChanged;
        }
        if (_clipGrid != null)
        {
            _clipGrid.SizeChanged += OnClipGridSizeChanged;
            UpdateManipulationMode();
            _clipGrid.ManipulationStarted += OnManipulationStarted;
            _clipGrid.ManipulationDelta += OnManipulationDelta;
            _clipGrid.ManipulationCompleted += OnManipulationCompleted;
        }

        if (_indicatorPanel != null)
        {
            _indicatorPanel.Orientation = Orientation;
        }

        ConfigureTrack();
        RebuildItems();
        ValidateSelectionBounds();
        UpdatePlacement(false);
        UpdateAccessibleNames();
        ShowCaption(false);
        UpdateAutoPlay();
    }

    private void OnClipGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_clipGrid != null)
            _clipGrid.Clip = new RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height)
            };
    }

    private void OnItemsPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isManipulating) SnapTrack();
    }

    // Re-parenting fires Unloaded then Loaded: everything released here is taken back in OnLoaded.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ObserveCollection(ItemsSource as INotifyCollectionChanged);
        UpdateAutoPlay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAutoPlay();
        CancelTransition();
        ObserveCollection(null);
    }

    private void ObserveCollection(INotifyCollectionChanged? collection)
    {
        if (ReferenceEquals(_observedCollection, collection)) return;
        if (_observedCollection != null) _observedCollection.CollectionChanged -= OnItemsCollectionChanged;
        _observedCollection = collection;
        if (_observedCollection != null) _observedCollection.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnNavButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender == _previousButton) Previous();
        else Next();
    }

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => IsPaused = !IsPaused;

    #region Navigation

    public void Next()
    {
        if (_isTransitioning || _itemCount == 0) return;
        var next = WrapIndex(_selectedIndex + 1);
        if (next >= 0) GoTo(next);
    }

    public void Previous()
    {
        if (_isTransitioning || _itemCount == 0) return;
        var prev = WrapIndex(_selectedIndex - 1);
        if (prev >= 0) GoTo(prev);
    }

    public void GoTo(int index, bool animate = true)
    {
        if (_itemCount == 0 || index < 0 || index >= _itemCount) return;
        if (index == _selectedIndex)
        {
            if (IsTrack) AnimateTrack(index, SettleDuration, Smooth());
            return;
        }

        CancelTransition();

        var previousItem = GetItemAt(_selectedIndex);
        var newItem = GetItemAt(index);

        var changingArgs = new CarouselSelectionChangingEventArgs(previousItem, newItem);
        SelectionChanging?.Invoke(this, changingArgs);
        if (changingArgs.Cancel)
        {
            if (IsTrack) AnimateTrack(_selectedIndex, SettleDuration, Smooth());
            return;
        }

        var previousIndex = _selectedIndex;
        var forward = index > previousIndex
            || (IsLoopingEnabled && previousIndex == _itemCount - 1 && index == 0);

        ApplySelection(index, newItem);
        UpdateNavigationButtonStates();

        if (IsTrack)
        {
            if (animate && previousIndex >= 0) AnimateTrack(index, GlideDuration, Glide());
            else SnapTrack();
            UpdateTrackOpacity(animate && previousIndex >= 0);
        }
        else
        {
            var fromElement = GetChildAt(previousIndex);
            var toElement = GetChildAt(index);

            if (toElement != null && animate && previousIndex >= 0)
            {
                _isTransitioning = true;
                _transition.Run(fromElement, toElement, forward, () =>
                {
                    _isTransitioning = false;
                    SetItemVisibility(index);
                });
            }
            else
            {
                SetItemVisibility(index);
            }
        }

        ShowCaption(animate && previousIndex >= 0);
        SelectionChanged?.Invoke(this, new CarouselSelectionChangedEventArgs(
            previousItem, newItem, previousIndex, index));
        StartCycle();
    }

    /// <summary>
    /// Wraps an index for looping. Returns -1 if out of bounds and looping is disabled.
    /// </summary>
    private int WrapIndex(int index)
    {
        if (index >= 0 && index < _itemCount) return index;
        if (!IsLoopingEnabled) return -1;
        if (index < 0) return _itemCount - 1;
        return 0;
    }

    private void ApplySelection(int index, object? item)
    {
        _isSynchronizingSelection = true;
        _selectedIndex = index;
        SelectedIndex = index;
        SelectedItem = item;
        _isSynchronizingSelection = false;
        if (_indicatorPanel != null) _indicatorPanel.SelectedIndex = index;
        for (int i = 0; i < _thumbnails.Count; i++) _thumbnails[i].IsChecked = i == index;
        UpdatePositionText();
    }

    private void ClearSelection()
    {
        _isSynchronizingSelection = true;
        _selectedIndex = -1;
        SelectedIndex = -1;
        SelectedItem = null;
        _isSynchronizingSelection = false;
        UpdatePositionText();
        ShowCaption(false);
    }

    private void CancelTransition()
    {
        if (_isTransitioning)
        {
            _transition.Cancel();
            _isTransitioning = false;
        }
    }

    #endregion

    #region Track

    private void ConfigureTrack()
    {
        if (_itemsPanel == null) return;
        _itemsPanel.Configure(ItemPeek, ItemSpacing, Orientation);
        if (!_itemsPanel.IsTrack)
        {
            StopTrackAnimation();
            _trackTransform.X = 0;
            _trackTransform.Y = 0;
        }
        SetItemVisibility(Math.Max(0, _selectedIndex));
    }

    private double TrackTarget(int index) => -Math.Max(0, index) * (_itemsPanel?.Pitch ?? 0);

    private double TrackOffset
    {
        get => Orientation == Orientation.Horizontal ? _trackTransform.X : _trackTransform.Y;
        set
        {
            if (Orientation == Orientation.Horizontal) { _trackTransform.X = value; _trackTransform.Y = 0; }
            else { _trackTransform.Y = value; _trackTransform.X = 0; }
        }
    }

    private void StopTrackAnimation()
    {
        if (_trackStoryboard == null) return;
        // A stopped storyboard drops back to the local value, so keep where the glide had got to.
        var current = TrackOffset;
        _trackStoryboard.Stop();
        _trackStoryboard = null;
        TrackOffset = current;
    }

    private void SnapTrack()
    {
        if (!IsTrack) return;
        StopTrackAnimation();
        TrackOffset = TrackTarget(_selectedIndex);
    }

    private void AnimateTrack(int index, TimeSpan duration, KeySpline spline)
    {
        StopTrackAnimation();
        var target = TrackTarget(index);
        var animation = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = true };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = duration, Value = target, KeySpline = spline });
        Storyboard.SetTarget(animation, _trackTransform);
        Storyboard.SetTargetProperty(animation, Orientation == Orientation.Horizontal ? "X" : "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            if (_trackStoryboard != storyboard) return;
            _trackStoryboard = null;
            storyboard.Stop();
            TrackOffset = target;
        };
        _trackStoryboard = storyboard;
        storyboard.Begin();
    }

    private void UpdateTrackOpacity(bool animate)
    {
        if (_itemsPanel == null || !IsTrack) return;

        _opacityStoryboard?.Stop();
        _opacityStoryboard = null;
        var storyboard = animate ? new Storyboard() : null;

        for (int i = 0; i < _itemsPanel.Children.Count; i++)
        {
            var child = _itemsPanel.Children[i];
            var target = i == _selectedIndex || _selectedIndex < 0 && i == 0 ? 1.0 : InactiveItemOpacity;
            if (storyboard == null || Math.Abs(child.Opacity - target) < 0.001)
            {
                child.Opacity = target;
                continue;
            }

            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = GlideDuration, Value = target, KeySpline = Glide() });
            Storyboard.SetTarget(animation, child);
            Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
            storyboard.Children.Add(animation);
            child.Opacity = target;
        }

        if (storyboard is { Children.Count: > 0 })
        {
            _opacityStoryboard = storyboard;
            storyboard.Completed += (_, _) => { if (_opacityStoryboard == storyboard) { storyboard.Stop(); _opacityStoryboard = null; } };
            storyboard.Begin();
        }
    }

    #endregion

    #region Item Management

    private object? GetItemAt(int index)
        => _itemsSource is IList list && index >= 0 && index < list.Count ? list[index] : null;

    private UIElement? GetChildAt(int index)
        => _itemsPanel != null && index >= 0 && index < _itemsPanel.Children.Count
            ? _itemsPanel.Children[index] : null;

    private void SetItemVisibility(int visibleIndex)
    {
        if (_itemsPanel == null) return;
        var track = IsTrack;
        for (int i = 0; i < _itemsPanel.Children.Count; i++)
        {
            var child = _itemsPanel.Children[i];
            var visible = i == visibleIndex;
            if (!track) child.Opacity = visible ? 1.0 : 0;
            Canvas.SetZIndex(child, visible ? 1 : 0);
            if (child.RenderTransform is TranslateTransform tt)
            {
                tt.X = 0;
                tt.Y = 0;
            }
        }
        if (track)
        {
            UpdateTrackOpacity(false);
            SnapTrack();
        }
    }

    private void OnSelectedIndexChanged(DependencyPropertyChangedEventArgs args)
    {
        if (!_isSynchronizingSelection && (int)args.NewValue is var idx && idx != _selectedIndex)
            GoTo(idx);
    }

    private void OnSelectedItemChanged(DependencyPropertyChangedEventArgs args)
    {
        if (!_isSynchronizingSelection && args.NewValue != null && _itemsSource is IList list)
        {
            var index = list.IndexOf(args.NewValue);
            if (index >= 0 && index != _selectedIndex) GoTo(index);
        }
    }

    private void OnItemsSourceChanged(DependencyPropertyChangedEventArgs args)
    {
        _itemsSource = args.NewValue as IList;
        if (_itemsSource == null && args.NewValue is IEnumerable enumerable)
        {
            _itemsSource = enumerable.Cast<object>().ToList();
        }

        ObserveCollection(IsLoaded ? args.NewValue as INotifyCollectionChanged : null);

        RebuildItems();
        ValidateSelectionBounds();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildItems();
        ValidateSelectionBounds();
    }

    private void ValidateSelectionBounds()
    {
        if (_itemCount == 0) ClearSelection();
        else if (_selectedIndex < 0 || _selectedIndex >= _itemCount)
        {
            // A SelectedIndex set before the items were built is honoured once they exist.
            var requested = _selectedIndex < 0 && SelectedIndex >= 0 ? SelectedIndex : _selectedIndex;
            _selectedIndex = -1;
            GoTo(Math.Min(_itemCount - 1, Math.Max(0, requested)), false);
        }
        else
        {
            ApplySelection(_selectedIndex, GetItemAt(_selectedIndex));
            ShowCaption(false);
            StartCycle();
        }
    }

    private void RebuildItems()
    {
        if (_itemsPanel == null) return;

        CancelTransition();
        _itemsPanel.Children.Clear();
        RebuildThumbnails();

        if (_itemsSource == null)
        {
            _itemCount = 0;
            if (_indicatorPanel != null)
            {
                _indicatorPanel.ItemCount = 0;
                _indicatorPanel.Visibility = Visibility.Collapsed;
            }
            UpdateNavigationButtonStates();
            UpdatePositionText();
            return;
        }

        var template = ItemTemplate;
        foreach (var item in _itemsSource)
        {
            FrameworkElement? content = null;
            if (template != null) content = template.LoadContent() as FrameworkElement;

            if (content != null)
                content.DataContext = item;
            else
                content = new ContentPresenter
                {
                    Content = item,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };

            _itemsPanel.Children.Add(content);
        }

        _itemCount = _itemsPanel.Children.Count;
        var selected = Math.Max(0, _selectedIndex);
        SetItemVisibility(selected);

        if (_indicatorPanel != null)
        {
            _indicatorPanel.ItemCount = _itemCount;
            _indicatorPanel.SelectedIndex = selected;
            _indicatorPanel.Visibility = _itemCount > 1 && ThumbnailTemplate == null ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateNavigationButtonStates();
        UpdatePositionText();
    }

    private void RebuildThumbnails()
    {
        foreach (var thumbnail in _thumbnails) thumbnail.Click -= OnThumbnailClick;
        _thumbnails.Clear();
        _thumbnailPanel?.Children.Clear();
        StopFill();

        if (_thumbnailPanel == null || ThumbnailTemplate is not DataTemplate template || _itemsSource == null)
        {
            if (_thumbnailPanel != null) _thumbnailPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var index = 0;
        foreach (var item in _itemsSource)
        {
            var content = template.LoadContent() as FrameworkElement;
            if (content != null) content.DataContext = item;

            var thumbnail = new CarouselThumbnail
            {
                Content = content,
                Tag = index,
                IsChecked = index == Math.Max(0, _selectedIndex),
            };
            if (ThumbnailStyle != null) thumbnail.Style = ThumbnailStyle;
            thumbnail.Click += OnThumbnailClick;
            _thumbnails.Add(thumbnail);
            _thumbnailPanel.Children.Add(thumbnail);
            index++;
        }

        _thumbnailPanel.Visibility = _thumbnails.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        UpdateThumbnailSizes();
        UpdateAccessibleNames();
        SetFills();
    }

    private void OnThumbnailClick(object sender, RoutedEventArgs e)
    {
        if (sender is CarouselThumbnail { Tag: int index }) GoTo(index);
    }

    private void UpdateThumbnailSizes()
    {
        foreach (var thumbnail in _thumbnails)
        {
            thumbnail.Width = ThumbnailWidth;
            // The thumbnail's template adds a 6 px gap and the 2 px progress bar under the image.
            thumbnail.Height = ThumbnailHeight + 8;
        }
    }

    #endregion

    #region Placement, labels and caption

    private void UpdatePlacement(bool useTransitions)
    {
        var bottom = CaptionPlacement switch
        {
            CarouselCaptionPlacement.Bottom => true,
            CarouselCaptionPlacement.Side => false,
            _ => ActualWidth > 0 && ActualWidth < SidePlacementMinWidth,
        };

        var changed = bottom != _isBottomPlacement;
        _isBottomPlacement = bottom;
        VisualStateManager.GoToState(this, bottom ? "Bottom" : "Side", useTransitions);
        if (GetTemplateChild("HeaderRow") is UIElement header)
            header.Visibility = bottom && Header != null ? Visibility.Visible : Visibility.Collapsed;
        if (_transportRow != null)
            _transportRow.Visibility = !bottom && TransportVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        if (changed) UpdatePositionText();
    }

    private void UpdatePositionText()
    {
        if (_positionText == null) return;
        _positionText.Inlines.Clear();
        if (_itemCount == 0) return;

        var number = (Math.Max(0, _selectedIndex) + 1).ToString("00");
        var total = _itemCount.ToString("00");
        if (_isBottomPlacement)
        {
            _positionText.Inlines.Add(new Run { Text = $"{number} / {total}" });
        }
        else
        {
            _positionText.Inlines.Add(new Run { Text = $"{ItemLabel.ToUpperInvariant()} " });
            var strong = new Run { Text = number, FontWeight = FontWeights.SemiBold };
            if (TryFindResource("CarouselStrongFontFamily") is FontFamily strongFamily) strong.FontFamily = strongFamily;
            strong.Foreground = Foreground;
            _positionText.Inlines.Add(strong);
            _positionText.Inlines.Add(new Run { Text = $" OF {total}" });
        }

        AutomationProperties.SetName(_positionText, $"{ItemLabel} {number} of {total}");
        if (IsLoaded)
            FrameworkElementAutomationPeer.FromElement(_positionText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private object? TryFindResource(string key)
    {
        for (DependencyObject? node = _positionText; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement fe && fe.Resources.TryGetValue(key, out var local)) return local;
        }
        return Application.Current.Resources.TryGetValue(key, out var value) ? value : null;
    }

    private void UpdateAccessibleNames()
    {
        var noun = ItemLabel.ToLowerInvariant();
        if (_previousButton != null) AutomationProperties.SetName(_previousButton, $"Previous {noun}");
        if (_nextButton != null) AutomationProperties.SetName(_nextButton, $"Next {noun}");
        for (int i = 0; i < _thumbnails.Count; i++)
        {
            var item = GetItemAt(i);
            var text = item?.ToString();
            var name = $"{ItemLabel} {i + 1}";
            if (!string.IsNullOrEmpty(text) && text != item!.GetType().FullName) name += $": {text}";
            AutomationProperties.SetName(_thumbnails[i], name);
        }
    }

    private void ShowCaption(bool animate)
    {
        if (_captionA == null || _captionB == null) return;

        var incoming = _captionAIsActive ? _captionB : _captionA;
        var outgoing = _captionAIsActive ? _captionA : _captionB;
        _captionAIsActive = !_captionAIsActive;

        _captionStoryboard?.Stop();
        _captionStoryboard = null;

        // Without a caption template there is no caption (a bare ContentPresenter would print the item).
        var caption = _selectedIndex >= 0 && CaptionTemplate != null ? GetItemAt(_selectedIndex) : null;
        incoming.ContentTemplate = caption != null ? CaptionTemplate : null;
        incoming.Content = caption;
        incoming.IsHitTestVisible = true;
        outgoing.IsHitTestVisible = false;
        Canvas.SetZIndex(incoming, 1);
        Canvas.SetZIndex(outgoing, 0);

        if (incoming.RenderTransform is not TranslateTransform rise)
            incoming.RenderTransform = rise = new TranslateTransform();

        if (!animate)
        {
            incoming.Opacity = 1;
            rise.Y = 0;
            outgoing.Opacity = 0;
            outgoing.Content = null;
            return;
        }

        incoming.Opacity = 0;
        rise.Y = 6;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Spline(outgoing, nameof(UIElement.Opacity), 0, TimeSpan.Zero, CaptionFade, Hover()));
        storyboard.Children.Add(Spline(incoming, nameof(UIElement.Opacity), 1, CaptionDelay, CaptionFade, Hover()));
        storyboard.Children.Add(Spline(rise, nameof(TranslateTransform.Y), 0, CaptionDelay, CaptionRise, Glide()));
        storyboard.Completed += (_, _) =>
        {
            if (_captionStoryboard != storyboard) return;
            storyboard.Stop();
            _captionStoryboard = null;
            incoming.Opacity = 1;
            rise.Y = 0;
            outgoing.Opacity = 0;
            outgoing.Content = null;
        };
        _captionStoryboard = storyboard;
        storyboard.Begin();
    }

    private static DoubleAnimationUsingKeyFrames Spline(DependencyObject target, string property, double to,
        TimeSpan delay, TimeSpan duration, KeySpline spline)
    {
        var animation = new DoubleAnimationUsingKeyFrames { BeginTime = delay, EnableDependentAnimation = true };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = duration, Value = to, KeySpline = spline });
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    #endregion

    #region Navigation Button State

    private void UpdateManipulationMode()
    {
        if (_clipGrid != null)
            _clipGrid.ManipulationMode = Orientation == Orientation.Horizontal
                ? ManipulationModes.TranslateX | ManipulationModes.TranslateInertia
                : ManipulationModes.TranslateY | ManipulationModes.TranslateInertia;
    }

    private void UpdateNavigationButtonStates()
    {
        if (_previousButton != null)
            _previousButton.IsEnabled = _itemCount > 1 && (IsLoopingEnabled || _selectedIndex > 0);
        if (_nextButton != null)
            _nextButton.IsEnabled = _itemCount > 1 && (IsLoopingEnabled || _selectedIndex < _itemCount - 1);
    }

    #endregion

    #region Swipe Gestures

    private static void EnsureTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is not TranslateTransform)
            element.RenderTransform = new TranslateTransform();
    }

    private void OnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (_isTransitioning || _itemCount <= 1) { e.Complete(); return; }
        _isManipulating = true;
        _peekElement = null;
        _peekIndex = -1;
        if (IsTrack)
        {
            StopTrackAnimation();
            _manipulationBase = TrackOffset;
        }
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (!_isManipulating || _clipGrid == null) return;

        var horizontal = Orientation == Orientation.Horizontal;
        var cumulative = horizontal ? e.Cumulative.Translation.X : e.Cumulative.Translation.Y;

        if (IsTrack)
        {
            var targetIndex = WrapIndex(cumulative < 0 ? _selectedIndex + 1 : _selectedIndex - 1);
            // Past the ends of a non-looping track the plates follow the finger at 30 %.
            TrackOffset = _manipulationBase + (targetIndex < 0 ? cumulative * 0.3 : cumulative);
            return;
        }

        var panelSize = horizontal ? _clipGrid.ActualWidth : _clipGrid.ActualHeight;
        if (panelSize <= 0) return;

        var currentElement = GetChildAt(_selectedIndex);
        if (currentElement == null) return;

        var rawTarget = cumulative < 0 ? _selectedIndex + 1 : _selectedIndex - 1;
        var overlayTarget = WrapIndex(rawTarget);
        var atBoundary = overlayTarget < 0;
        var translation = atBoundary ? cumulative * 0.3 : cumulative;

        EnsureTranslateTransform(currentElement);
        SetTranslation(currentElement, translation, horizontal);

        if (!atBoundary)
        {
            if (_peekIndex != overlayTarget)
            {
                HidePeek();
                _peekIndex = overlayTarget;
                _peekElement = GetChildAt(overlayTarget);
                if (_peekElement != null)
                {
                    EnsureTranslateTransform(_peekElement);
                    _peekElement.Opacity = 1;
                    Canvas.SetZIndex(_peekElement, 0);
                    Canvas.SetZIndex(currentElement, 1);
                }
            }

            if (_peekElement != null)
            {
                var peekOffset = (cumulative < 0 ? panelSize : -panelSize) + translation;
                SetTranslation(_peekElement, peekOffset, horizontal);
            }
        }
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (!_isManipulating || _clipGrid == null) return;
        _isManipulating = false;

        var horizontal = Orientation == Orientation.Horizontal;
        var cumulative = horizontal ? e.Cumulative.Translation.X : e.Cumulative.Translation.Y;
        var velocity = horizontal ? e.Velocities.Linear.X : e.Velocities.Linear.Y;
        var targetIndex = WrapIndex(cumulative < 0 ? _selectedIndex + 1 : _selectedIndex - 1);

        if (IsTrack)
        {
            if ((Math.Abs(cumulative) > SwipeThreshold || Math.Abs(velocity) > 0.5) && targetIndex >= 0)
                GoTo(targetIndex);
            else
                AnimateTrack(_selectedIndex, SettleDuration, Smooth());
            return;
        }

        var panelSize = horizontal ? _clipGrid.ActualWidth : _clipGrid.ActualHeight;
        if (panelSize <= 0) { CleanUpManipulation(); return; }

        var thresholdMet = Math.Abs(cumulative) > panelSize * 0.25 || Math.Abs(velocity) > 0.5;

        if (thresholdMet && targetIndex >= 0)
            CommitSwipeNavigation(targetIndex, cumulative < 0, panelSize, horizontal);
        else
            AnimateSnapBack(horizontal, panelSize);
    }

    private void CommitSwipeNavigation(int targetIndex, bool forward, double panelSize, bool horizontal)
    {
        _isTransitioning = true;
        var storyboard = new Storyboard();
        var prop = horizontal ? nameof(TranslateTransform.X) : nameof(TranslateTransform.Y);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(200));

        var currentElement = GetChildAt(_selectedIndex);
        if (currentElement != null)
        {
            EnsureTranslateTransform(currentElement);
            AddTranslateAnimation(storyboard, currentElement, GetTranslation(currentElement, horizontal),
                forward ? -panelSize : panelSize, duration, easing, prop);
        }

        var peekElement = GetChildAt(targetIndex);
        if (peekElement != null)
        {
            EnsureTranslateTransform(peekElement);
            AddTranslateAnimation(storyboard, peekElement, GetTranslation(peekElement, horizontal),
                0, duration, easing, prop);
        }

        var captured = targetIndex;
        storyboard.Completed += (_, _) =>
        {
            _isTransitioning = false;
            _peekElement = null;
            _peekIndex = -1;

            var previousItem = GetItemAt(_selectedIndex);
            var newItem = GetItemAt(captured);
            var previousIndex = _selectedIndex;

            ApplySelection(captured, newItem);
            SetItemVisibility(captured);
            UpdateNavigationButtonStates();
            ShowCaption(true);

            SelectionChanged?.Invoke(this, new CarouselSelectionChangedEventArgs(
                previousItem, newItem, previousIndex, captured));
            StartCycle();
        };
        storyboard.Begin();
    }

    private void AnimateSnapBack(bool horizontal, double panelSize)
    {
        _isTransitioning = true;
        var storyboard = new Storyboard();
        var prop = horizontal ? nameof(TranslateTransform.X) : nameof(TranslateTransform.Y);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(250));

        var currentElement = GetChildAt(_selectedIndex);
        if (currentElement != null)
        {
            EnsureTranslateTransform(currentElement);
            AddTranslateAnimation(storyboard, currentElement, GetTranslation(currentElement, horizontal),
                0, duration, easing, prop);
        }

        if (_peekElement != null && _peekIndex >= 0)
        {
            EnsureTranslateTransform(_peekElement);
            var pos = GetTranslation(_peekElement, horizontal);
            AddTranslateAnimation(storyboard, _peekElement, pos,
                pos > 0 ? panelSize : -panelSize, duration, easing, prop);
        }

        storyboard.Completed += (_, _) =>
        {
            _isTransitioning = false;
            CleanUpManipulation();
            SetItemVisibility(_selectedIndex);
        };
        storyboard.Begin();
    }

    private static void AddTranslateAnimation(Storyboard sb, UIElement element,
        double from, double to, Duration duration, EasingFunctionBase easing, string property)
    {
        var anim = new DoubleAnimation
        {
            From = from, To = to, Duration = duration,
            EasingFunction = easing, EnableDependentAnimation = true
        };
        Storyboard.SetTarget(anim, element.RenderTransform);
        Storyboard.SetTargetProperty(anim, property);
        sb.Children.Add(anim);
    }

    private static void SetTranslation(UIElement element, double value, bool horizontal)
    {
        if (element.RenderTransform is TranslateTransform tt)
        {
            if (horizontal) tt.X = value; else tt.Y = value;
        }
    }

    private static double GetTranslation(UIElement element, bool horizontal)
        => element.RenderTransform is TranslateTransform tt
            ? (horizontal ? tt.X : tt.Y) : 0;

    private void HidePeek()
    {
        if (_peekElement != null && _peekIndex != _selectedIndex)
        {
            _peekElement.Opacity = 0;
            Canvas.SetZIndex(_peekElement, 0);
        }
    }

    private void CleanUpManipulation()
    {
        HidePeek();
        if (_peekElement?.RenderTransform is TranslateTransform tt) { tt.X = 0; tt.Y = 0; }
        _peekElement = null;
        _peekIndex = -1;
    }

    #endregion

    #region Keyboard and focus

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left when Orientation == Orientation.Horizontal:
            case Windows.System.VirtualKey.Up when Orientation == Orientation.Vertical:
                Previous(); e.Handled = true; break;
            case Windows.System.VirtualKey.Right when Orientation == Orientation.Horizontal:
            case Windows.System.VirtualKey.Down when Orientation == Orientation.Vertical:
                Next(); e.Handled = true; break;
            case Windows.System.VirtualKey.Home:
                GoTo(0); e.Handled = true; break;
            case Windows.System.VirtualKey.End:
                GoTo(_itemCount - 1); e.Handled = true; break;
            // Space on the carousel itself pauses; on a focused button it stays that button's click.
            case Windows.System.VirtualKey.Space when ReferenceEquals(e.OriginalSource, this) && IsAutoPlayEnabled:
                IsPaused = !IsPaused; e.Handled = true; break;
        }
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!_focusWithin) { _focusWithin = true; UpdateHold(); }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        var focused = XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null;
        for (var node = focused; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, this)) return;
        }
        if (_focusWithin) { _focusWithin = false; UpdateHold(); }
    }

    #endregion

    #region Auto-Play

    private void UpdateAutoPlay()
    {
        if (IsAutoPlayActive)
        {
            if (_autoPlayTimer == null)
            {
                _autoPlayTimer = new DispatcherTimer();
                _autoPlayTimer.Tick += OnAutoPlayTick;
            }
            StartCycle();
        }
        else
        {
            StopAutoPlay();
        }
    }

    private void StopAutoPlay()
    {
        if (_autoPlayTimer != null)
        {
            _autoPlayTimer.Stop();
            _autoPlayTimer.Tick -= OnAutoPlayTick;
            _autoPlayTimer = null;
        }
        _timerRunning = false;
        StopFill();
        SetFills();
        UpdatePlaybackState();
    }

    private void OnAutoPlayTick(object? sender, object e)
    {
        _autoPlayTimer?.Stop();
        _timerRunning = false;
        if (!IsLoopingEnabled && _selectedIndex >= _itemCount - 1)
        {
            StopFill();
            SetFills();
            return;
        }
        var before = _selectedIndex;
        Next();
        if (_selectedIndex == before) StartCycle();
    }

    /// <summary>Starts a full interval for the current item: timer, progress bar and status.</summary>
    private void StartCycle()
    {
        StopFill();
        _autoPlayTimer?.Stop();
        _timerRunning = false;

        var interval = AutoPlayInterval;
        if (!IsAutoPlayActive || _autoPlayTimer == null)
        {
            SetFills();
            UpdatePlaybackState();
            return;
        }

        _remaining = interval;
        SetFills();
        BeginFill(interval);
        if (IsHeld)
        {
            _fillStoryboard?.Pause();
        }
        else
        {
            _segmentStart = Stopwatch.GetTimestamp();
            _autoPlayTimer.Interval = _remaining;
            _autoPlayTimer.Start();
            _timerRunning = true;
        }
        UpdatePlaybackState();
    }

    /// <summary>Holds or resumes the running interval (pointer over, focus inside, paused), keeping the time left.</summary>
    private void UpdateHold()
    {
        if (IsAutoPlayActive && _autoPlayTimer != null)
        {
            if (IsHeld && _timerRunning)
            {
                _autoPlayTimer.Stop();
                _timerRunning = false;
                _remaining -= Stopwatch.GetElapsedTime(_segmentStart);
                if (_remaining < TimeSpan.Zero) _remaining = TimeSpan.Zero;
                _fillStoryboard?.Pause();
            }
            else if (!IsHeld && !_timerRunning)
            {
                _segmentStart = Stopwatch.GetTimestamp();
                _autoPlayTimer.Interval = _remaining > TimeSpan.FromMilliseconds(50) ? _remaining : TimeSpan.FromMilliseconds(50);
                _autoPlayTimer.Start();
                _timerRunning = true;
                _fillStoryboard?.Resume();
            }
        }
        UpdatePlaybackState();
    }

    private void UpdatePlaybackState()
    {
        var state = !IsAutoPlayActive ? CarouselPlaybackState.Off
            : IsPaused ? CarouselPlaybackState.Paused
            : IsHeld ? CarouselPlaybackState.Held
            : CarouselPlaybackState.Playing;
        PlaybackState = state;
        VisualStateManager.GoToState(this, state.ToString(), true);

        if (_statusText != null)
        {
            _statusText.Text = state switch
            {
                CarouselPlaybackState.Paused => "Paused",
                CarouselPlaybackState.Held => "Held",
                CarouselPlaybackState.Playing => $"Auto · {AutoPlayInterval.TotalSeconds:0.#} s",
                _ => string.Empty,
            };
        }
        if (_playPauseButton != null)
            AutomationProperties.SetName(_playPauseButton, IsPaused ? "Play slideshow" : "Pause slideshow");
    }

    /// <summary>Done items show a full bar, later ones an empty bar; the current one is full without auto-play.</summary>
    private void SetFills()
    {
        var selected = Math.Max(0, _selectedIndex);
        var running = IsAutoPlayActive;
        for (int i = 0; i < _thumbnails.Count; i++)
        {
            _thumbnails[i].ProgressScale.ScaleX = i < selected || (i == selected && !running) ? 1 : 0;
        }
    }

    private void BeginFill(TimeSpan duration)
    {
        var selected = Math.Max(0, _selectedIndex);
        if (selected >= _thumbnails.Count) return;

        var animation = new DoubleAnimation { From = 0, To = 1, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(animation, _thumbnails[selected].ProgressScale);
        Storyboard.SetTargetProperty(animation, nameof(ScaleTransform.ScaleX));
        _fillStoryboard = new Storyboard();
        _fillStoryboard.Children.Add(animation);
        _fillStoryboard.Begin();
    }

    private void StopFill()
    {
        _fillStoryboard?.Stop();
        _fillStoryboard = null;
    }

    #endregion

    #region Pointer Interaction (hold auto-play on hover)

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch && !_pointerOver)
        {
            _pointerOver = true;
            UpdateHold();
        }
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Pressing the items focuses the carousel, so the arrow keys and Space work straight away.
        if (!e.Handled && FocusState == FocusState.Unfocused) Focus(FocusState.Pointer);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerOver)
        {
            _pointerOver = false;
            UpdateHold();
        }
    }

    #endregion
}
