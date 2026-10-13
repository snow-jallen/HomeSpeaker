namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Picks the overlay arrangement that covers the fewest faces. The default arrangement has the
/// clock top-left, chips top-right and now-playing bottom-left; mirrored swaps left and right.
/// Face boxes are mapped through the object-fit: cover crop of a 1024x600 screen first.
/// </summary>
public static class OverlayLayoutChooser
{
    private const double ScreenAspect = 1024.0 / 600.0;

    // Normalized screen regions for the default layout: clock+date, chips, now-playing panel.
    private static readonly NormalizedRect[] defaultRegions =
    [
        new(0.03, 0.04, 0.42, 0.30),
        new(0.66, 0.04, 0.97, 0.40),
        new(0.03, 0.70, 0.55, 0.96),
    ];

    public static OverlayLayout Choose(PhotoEntry photo)
    {
        if (photo.Faces.Count == 0 || photo.ImageWidth <= 0 || photo.ImageHeight <= 0)
        {
            return OverlayLayout.Default;
        }

        var imageAspect = (double)photo.ImageWidth / photo.ImageHeight;
        var faces = photo.Faces.Select(f => toScreen(f, imageAspect)).ToList();
        var defaultOverlap = totalOverlap(defaultRegions, faces);
        var mirroredOverlap = totalOverlap(defaultRegions.Select(mirror), faces);
        return mirroredOverlap < defaultOverlap ? OverlayLayout.Mirrored : OverlayLayout.Default;
    }

    private static NormalizedRect toScreen(FaceBox face, double imageAspect)
    {
        if (imageAspect > ScreenAspect)
        {
            // Wider than the screen: full height visible, left and right edges cropped.
            var visibleWidth = ScreenAspect / imageAspect;
            var offset = (1 - visibleWidth) / 2;
            return new((face.X1 - offset) / visibleWidth, face.Y1, (face.X2 - offset) / visibleWidth, face.Y2);
        }

        // Taller than the screen: full width visible, top and bottom cropped.
        var visibleHeight = imageAspect / ScreenAspect;
        var offsetY = (1 - visibleHeight) / 2;
        return new(face.X1, (face.Y1 - offsetY) / visibleHeight, face.X2, (face.Y2 - offsetY) / visibleHeight);
    }

    private static NormalizedRect mirror(NormalizedRect r) => new(1 - r.X2, r.Y1, 1 - r.X1, r.Y2);

    private static double totalOverlap(IEnumerable<NormalizedRect> regions, List<NormalizedRect> faces) =>
        regions.Sum(region => faces.Sum(face => intersection(region, face)));

    private static double intersection(NormalizedRect a, NormalizedRect b)
    {
        var width = Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1);
        var height = Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1);
        return width > 0 && height > 0 ? width * height : 0;
    }

    private readonly record struct NormalizedRect(double X1, double Y1, double X2, double Y2);
}
