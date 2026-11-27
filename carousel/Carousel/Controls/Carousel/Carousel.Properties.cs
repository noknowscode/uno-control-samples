namespace Uno.Toolkit.UI;

public partial class Carousel
{
    public static DependencyProperty ItemsSourceProperty { get; } = DependencyProperty.Register(
        nameof(ItemsSource), typeof(object), typeof(Carousel),
        new PropertyMetadata(null, (s, e) => ((Carousel)s).OnItemsSourceChanged(e)));

    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static DependencyProperty ItemTemplateProperty { get; } = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(Carousel),
        new PropertyMetadata(null, (s, e) => ((Carousel)s).RebuildItems()));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static DependencyProperty SelectedIndexProperty { get; } = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(Carousel),
        new PropertyMetadata(-1, (s, e) => ((Carousel)s).OnSelectedIndexChanged(e)));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static DependencyProperty SelectedItemProperty { get; } = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(Carousel),
        new PropertyMetadata(null, (s, e) => ((Carousel)s).OnSelectedItemChanged(e)));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static DependencyProperty ItemTransitionProperty { get; } = DependencyProperty.Register(
        nameof(ItemTransition), typeof(ICarouselTransition), typeof(Carousel),
        new PropertyMetadata(null, (s, e) =>
        {
            var c = (Carousel)s;
            (e.OldValue as ICarouselTransition)?.Cancel();
            c._transition = e.NewValue as ICarouselTransition ?? new SlideTransition();
        }));

    public ICarouselTransition? ItemTransition
    {
        get => (ICarouselTransition?)GetValue(ItemTransitionProperty);
        set => SetValue(ItemTransitionProperty, value);
    }

    public static DependencyProperty OrientationProperty { get; } = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(Carousel),
        new PropertyMetadata(Orientation.Horizontal, (s, e) =>
        {
            var c = (Carousel)s;
            if (c._indicatorPanel != null) c._indicatorPanel.Orientation = c.Orientation;
            c.UpdateManipulationMode();
            c.ConfigureTrack();
        }));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static DependencyProperty IsLoopingEnabledProperty { get; } = DependencyProperty.Register(
        nameof(IsLoopingEnabled), typeof(bool), typeof(Carousel),
        new PropertyMetadata(true, (s, _) => ((Carousel)s).UpdateNavigationButtonStates()));

    public bool IsLoopingEnabled
    {
        get => (bool)GetValue(IsLoopingEnabledProperty);
        set => SetValue(IsLoopingEnabledProperty, value);
    }

    public static DependencyProperty AutoPlayIntervalProperty { get; } = DependencyProperty.Register(
        nameof(AutoPlayInterval), typeof(TimeSpan), typeof(Carousel),
        new PropertyMetadata(TimeSpan.FromSeconds(6), (s, _) => ((Carousel)s).UpdateAutoPlay()));

    /// <summary>Time each item stays before auto-play advances (6 s by default).</summary>
    public TimeSpan AutoPlayInterval
    {
        get => (TimeSpan)GetValue(AutoPlayIntervalProperty);
        set => SetValue(AutoPlayIntervalProperty, value);
    }

    public static DependencyProperty IsAutoPlayEnabledProperty { get; } = DependencyProperty.Register(
        nameof(IsAutoPlayEnabled), typeof(bool), typeof(Carousel),
        new PropertyMetadata(false, (s, _) => ((Carousel)s).UpdateAutoPlay()));

    public bool IsAutoPlayEnabled
    {
        get => (bool)GetValue(IsAutoPlayEnabledProperty);
        set => SetValue(IsAutoPlayEnabledProperty, value);
    }

    public static DependencyProperty IndicatorVisibilityProperty { get; } = DependencyProperty.Register(
        nameof(IndicatorVisibility), typeof(Visibility), typeof(Carousel),
        new PropertyMetadata(Visibility.Visible));

    public Visibility IndicatorVisibility
    {
        get => (Visibility)GetValue(IndicatorVisibilityProperty);
        set => SetValue(IndicatorVisibilityProperty, value);
    }

    public static DependencyProperty IndicatorStyleProperty { get; } = DependencyProperty.Register(
        nameof(IndicatorStyle), typeof(Style), typeof(Carousel),
        new PropertyMetadata(null));

    public Style? IndicatorStyle
    {
        get => (Style?)GetValue(IndicatorStyleProperty);
        set => SetValue(IndicatorStyleProperty, value);
    }

    public static DependencyProperty ThumbnailStyleProperty { get; } = DependencyProperty.Register(
        nameof(ThumbnailStyle), typeof(Style), typeof(Carousel),
        new PropertyMetadata(null, (s, _) => ((Carousel)s).RebuildItems()));

    /// <summary>Style of each <see cref="CarouselThumbnail"/> in the thumbnail strip.</summary>
    public Style? ThumbnailStyle
    {
        get => (Style?)GetValue(ThumbnailStyleProperty);
        set => SetValue(ThumbnailStyleProperty, value);
    }

    public static DependencyProperty ItemPeekProperty { get; } = DependencyProperty.Register(
        nameof(ItemPeek), typeof(double), typeof(Carousel),
        new PropertyMetadata(0d, (s, _) => ((Carousel)s).ConfigureTrack()));

    /// <summary>
    /// How much of the next item shows at the trailing edge. A peek or a spacing above 0 switches the carousel to
    /// track mode: the items sit in a row and glide together (<see cref="ItemTransition"/> is not used).
    /// </summary>
    public double ItemPeek
    {
        get => (double)GetValue(ItemPeekProperty);
        set => SetValue(ItemPeekProperty, value);
    }

    public static DependencyProperty ItemSpacingProperty { get; } = DependencyProperty.Register(
        nameof(ItemSpacing), typeof(double), typeof(Carousel),
        new PropertyMetadata(0d, (s, _) => ((Carousel)s).ConfigureTrack()));

    /// <summary>Gap between two items in track mode.</summary>
    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public static DependencyProperty InactiveItemOpacityProperty { get; } = DependencyProperty.Register(
        nameof(InactiveItemOpacity), typeof(double), typeof(Carousel),
        new PropertyMetadata(1d, (s, _) => ((Carousel)s).UpdateTrackOpacity(false)));

    /// <summary>Opacity of the items that are not selected, in track mode.</summary>
    public double InactiveItemOpacity
    {
        get => (double)GetValue(InactiveItemOpacityProperty);
        set => SetValue(InactiveItemOpacityProperty, value);
    }

    public static DependencyProperty ThumbnailTemplateProperty { get; } = DependencyProperty.Register(
        nameof(ThumbnailTemplate), typeof(DataTemplate), typeof(Carousel),
        new PropertyMetadata(null, (s, _) => ((Carousel)s).RebuildItems()));

    /// <summary>
    /// When set, a strip of thumbnails (one per item, each with an auto-play progress bar) replaces the dots.
    /// </summary>
    public DataTemplate? ThumbnailTemplate
    {
        get => (DataTemplate?)GetValue(ThumbnailTemplateProperty);
        set => SetValue(ThumbnailTemplateProperty, value);
    }

    public static DependencyProperty ThumbnailWidthProperty { get; } = DependencyProperty.Register(
        nameof(ThumbnailWidth), typeof(double), typeof(Carousel),
        new PropertyMetadata(44d, (s, _) => ((Carousel)s).UpdateThumbnailSizes()));

    public double ThumbnailWidth
    {
        get => (double)GetValue(ThumbnailWidthProperty);
        set => SetValue(ThumbnailWidthProperty, value);
    }

    public static DependencyProperty ThumbnailHeightProperty { get; } = DependencyProperty.Register(
        nameof(ThumbnailHeight), typeof(double), typeof(Carousel),
        new PropertyMetadata(44d, (s, _) => ((Carousel)s).UpdateThumbnailSizes()));

    public double ThumbnailHeight
    {
        get => (double)GetValue(ThumbnailHeightProperty);
        set => SetValue(ThumbnailHeightProperty, value);
    }

    public static DependencyProperty CaptionTemplateProperty { get; } = DependencyProperty.Register(
        nameof(CaptionTemplate), typeof(DataTemplate), typeof(Carousel),
        new PropertyMetadata(null, (s, _) => ((Carousel)s).ShowCaption(false)));

    /// <summary>Template for the selected item's caption, shown beside or below the items and cross-faded.</summary>
    public DataTemplate? CaptionTemplate
    {
        get => (DataTemplate?)GetValue(CaptionTemplateProperty);
        set => SetValue(CaptionTemplateProperty, value);
    }

    public static DependencyProperty CaptionPlacementProperty { get; } = DependencyProperty.Register(
        nameof(CaptionPlacement), typeof(CarouselCaptionPlacement), typeof(Carousel),
        new PropertyMetadata(CarouselCaptionPlacement.Auto, (s, _) => ((Carousel)s).UpdatePlacement(true)));

    /// <summary>Where the caption, position label and thumbnails go. Auto puts them beside the items from
    /// <see cref="SidePlacementMinWidth"/> up and below them under it.</summary>
    public CarouselCaptionPlacement CaptionPlacement
    {
        get => (CarouselCaptionPlacement)GetValue(CaptionPlacementProperty);
        set => SetValue(CaptionPlacementProperty, value);
    }

    public static DependencyProperty SidePlacementMinWidthProperty { get; } = DependencyProperty.Register(
        nameof(SidePlacementMinWidth), typeof(double), typeof(Carousel),
        new PropertyMetadata(720d, (s, _) => ((Carousel)s).UpdatePlacement(true)));

    public double SidePlacementMinWidth
    {
        get => (double)GetValue(SidePlacementMinWidthProperty);
        set => SetValue(SidePlacementMinWidthProperty, value);
    }

    public static DependencyProperty HeaderProperty { get; } = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(Carousel),
        new PropertyMetadata(null, (s, _) => ((Carousel)s).UpdatePlacement(false)));

    /// <summary>Title shown above the items when the caption sits below them (the narrow layout).</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static DependencyProperty ItemLabelProperty { get; } = DependencyProperty.Register(
        nameof(ItemLabel), typeof(string), typeof(Carousel),
        new PropertyMetadata("Item", (s, _) => { var c = (Carousel)s; c.UpdatePositionText(); c.UpdateAccessibleNames(); }));

    /// <summary>Noun for one item, used in the position label ("PLATE 02 OF 05") and the accessible names.</summary>
    public string ItemLabel
    {
        get => (string)GetValue(ItemLabelProperty);
        set => SetValue(ItemLabelProperty, value);
    }

    public static DependencyProperty TransportVisibilityProperty { get; } = DependencyProperty.Register(
        nameof(TransportVisibility), typeof(Visibility), typeof(Carousel),
        new PropertyMetadata(Visibility.Visible, (s, _) => ((Carousel)s).UpdatePlacement(false)));

    /// <summary>Shows the previous / play-pause / next row in the side layout (the narrow layout never shows it).</summary>
    public Visibility TransportVisibility
    {
        get => (Visibility)GetValue(TransportVisibilityProperty);
        set => SetValue(TransportVisibilityProperty, value);
    }

    public static DependencyProperty IsPausedProperty { get; } = DependencyProperty.Register(
        nameof(IsPaused), typeof(bool), typeof(Carousel),
        new PropertyMetadata(false, (s, _) => ((Carousel)s).UpdateHold()));

    /// <summary>Pauses auto-play (the play/pause button and Space toggle it).</summary>
    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    public static DependencyProperty PlaybackStateProperty { get; } = DependencyProperty.Register(
        nameof(PlaybackState), typeof(CarouselPlaybackState), typeof(Carousel),
        new PropertyMetadata(CarouselPlaybackState.Off));

    /// <summary>Read-only: Off (no auto-play), Playing, Held (pointer over or focus inside) or Paused.</summary>
    public CarouselPlaybackState PlaybackState
    {
        get => (CarouselPlaybackState)GetValue(PlaybackStateProperty);
        private set => SetValue(PlaybackStateProperty, value);
    }
}

public enum CarouselCaptionPlacement
{
    Auto,
    Side,
    Bottom,
}

public enum CarouselPlaybackState
{
    Off,
    Playing,
    Held,
    Paused,
}
