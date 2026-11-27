using System.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CarouselDemo;

/// <summary>One plate of the "Low Tide Series": a bundled image and its caption.</summary>
public sealed class Plate : INotifyPropertyChanged
{
    // Deliberately not bundled: Break() points the plate here to show the failed state.
    private static readonly Uri MissingImage = new("ms-appx:///Assets/Plates/missing.png");

    private readonly Uri _source;
    private ImageSource _image;

    public Plate(string file, string title, string description, string alt)
    {
        _source = new Uri($"ms-appx:///Assets/Plates/{file}.png");
        _image = new BitmapImage(_source);
        Thumbnail = new BitmapImage(_source);
        Title = title;
        Description = description;
        Alt = alt;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string Description { get; }

    public string Medium => "Gouache on cotton paper · 30 × 20 cm";

    public string Alt { get; }

    public ImageSource Thumbnail { get; }

    public ImageSource Image
    {
        get => _image;
        private set
        {
            _image = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        }
    }

    public void Break() => Image = new BitmapImage(MissingImage);

    public void Restore() => Image = new BitmapImage(_source);

    public override string ToString() => Title;

    public static IReadOnlyList<Plate> LowTideSeries() =>
    [
        new("dune-field-morning", "Dune Field, Morning", "Five bands of sand under a low clay sun.",
            "Clay sun over ochre dunes"),
        new("coast-at-low-tide", "Coast at Low Tide", "Low water leaves three bands: slate, rust and the darker wet sand.",
            "Ochre sun over a slate sea and a rust shore"),
        new("ridge-evening", "Ridge, Evening", "Two ridgelines lose their colour an hour after sunset.",
            "Pale moon above layered dark ridges"),
        new("salt-marsh", "Salt Marsh", "Sage reeds at both edges frame a still channel.",
            "Reeds framing a slate channel below a treeline"),
        new("headland-at-noon", "Headland at Noon", "A clay cliff cut against a flat noon sea.",
            "Clay cliff with a sage crown over a slate sea"),
    ];
}
