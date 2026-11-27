namespace CarouselDemo.HotDesignPreviews;

internal static class PreviewData
{
    public static IReadOnlyList<Plate> Plates() => Plate.LowTideSeries();
}
