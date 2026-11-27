using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Uno.Toolkit.UI;

/// <summary>The load state of a <see cref="CarouselPlate"/>.</summary>
public enum CarouselPlateState
{
    Ready,
    Loading,
    Failed,
}

/// <summary>
/// A mounted image for a <see cref="Carousel"/> item: a mat with a hairline ring and a soft shadow, the image
/// cropped to fill it, and an optional caption below. <see cref="State"/> follows the image's load events
/// (Loading, then Ready or Failed) and can also be set by the app. In the Failed state a "Try again" button raises
/// <see cref="RetryRequested"/> and reloads the image.
/// </summary>
[TemplatePart(Name = PART_Image, Type = typeof(Image))]
[TemplatePart(Name = PART_RetryButton, Type = typeof(ButtonBase))]
[TemplateVisualState(GroupName = "PlateStates", Name = "Ready")]
[TemplateVisualState(GroupName = "PlateStates", Name = "Loading")]
[TemplateVisualState(GroupName = "PlateStates", Name = "Failed")]
[TemplateVisualState(GroupName = "CaptionStates", Name = "NoCaption")]
[TemplateVisualState(GroupName = "CaptionStates", Name = "WithCaption")]
public partial class CarouselPlate : Control
{
    private const string PART_Image = "PART_Image";
    private const string PART_RetryButton = "PART_RetryButton";

    private Image? _image;
    private ButtonBase? _retryButton;

    public CarouselPlate()
    {
        DefaultStyleKey = typeof(CarouselPlate);
    }

    public event EventHandler? RetryRequested;

    public static DependencyProperty SourceProperty { get; } = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(CarouselPlate),
        new PropertyMetadata(null, (d, _) => ((CarouselPlate)d).LoadImage()));

    /// <summary>The image shown on the mat. Setting it puts the plate in the Loading state until it opens.</summary>
    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static DependencyProperty StateProperty { get; } = DependencyProperty.Register(
        nameof(State), typeof(CarouselPlateState), typeof(CarouselPlate),
        new PropertyMetadata(CarouselPlateState.Ready, (d, _) => ((CarouselPlate)d).UpdateStates(true)));

    public CarouselPlateState State
    {
        get => (CarouselPlateState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static DependencyProperty TitleProperty { get; } = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CarouselPlate),
        new PropertyMetadata(null, (d, _) => ((CarouselPlate)d).UpdateStates(false)));

    /// <summary>Optional caption title under the mat: two lines at most, then an ellipsis.</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static DependencyProperty SubtitleProperty { get; } = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(CarouselPlate), new PropertyMetadata(null));

    /// <summary>Optional line under the title (medium, size, date).</summary>
    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static DependencyProperty FailedTextProperty { get; } = DependencyProperty.Register(
        nameof(FailedText), typeof(string), typeof(CarouselPlate), new PropertyMetadata("Image unavailable"));

    public string FailedText
    {
        get => (string)GetValue(FailedTextProperty);
        set => SetValue(FailedTextProperty, value);
    }

    public static DependencyProperty RetryTextProperty { get; } = DependencyProperty.Register(
        nameof(RetryText), typeof(string), typeof(CarouselPlate), new PropertyMetadata("Try again"));

    public string RetryText
    {
        get => (string)GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_image != null)
        {
            _image.ImageOpened -= OnImageOpened;
            _image.ImageFailed -= OnImageFailed;
        }
        if (_retryButton != null) _retryButton.Click -= OnRetryClick;

        _image = GetTemplateChild(PART_Image) as Image;
        _retryButton = GetTemplateChild(PART_RetryButton) as ButtonBase;

        if (_image != null)
        {
            _image.ImageOpened += OnImageOpened;
            _image.ImageFailed += OnImageFailed;
        }
        if (_retryButton != null) _retryButton.Click += OnRetryClick;

        LoadImage();
        UpdateStates(false);
    }

    /// <summary>Loads <see cref="Source"/> again, for example after the app fixed what made it fail.</summary>
    public void Reload()
    {
        if (_image != null) _image.Source = null;
        LoadImage();
    }

    private void LoadImage()
    {
        if (_image == null) return;
        var source = Source;
        if (source == null)
        {
            _image.Source = null;
            return;
        }

        State = CarouselPlateState.Loading;
        _image.Source = source;
        if (source is BitmapImage { PixelWidth: > 0 })
        {
            State = CarouselPlateState.Ready;
        }
    }

    private void OnImageOpened(object sender, RoutedEventArgs e) => State = CarouselPlateState.Ready;

    private void OnImageFailed(object sender, ExceptionRoutedEventArgs e) => State = CarouselPlateState.Failed;

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    private void UpdateStates(bool useTransitions)
    {
        VisualStateManager.GoToState(this, State.ToString(), useTransitions);
        VisualStateManager.GoToState(this, string.IsNullOrEmpty(Title) ? "NoCaption" : "WithCaption", useTransitions);
    }
}
