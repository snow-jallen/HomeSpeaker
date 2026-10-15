namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// How the slideshow fits a tall photo onto the wide screen. One is picked at random per slide while we
/// find out which looks best: the whole photo over a blurred copy of itself, two tall photos side by side,
/// a crop that keeps the faces in frame, or a slow pan from top to bottom.
/// </summary>
public static class PortraitStyle
{
    public const string Blur = "blur";
    public const string Pair = "pair";
    public const string Faces = "faces";
    public const string Pan = "pan";

    public static IReadOnlyList<string> All { get; } = [Blur, Pair, Faces, Pan];
}
