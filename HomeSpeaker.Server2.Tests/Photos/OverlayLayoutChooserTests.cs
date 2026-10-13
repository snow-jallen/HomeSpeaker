using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class OverlayLayoutChooserTests
{
    private static PhotoEntry photo(int width, int height, params FaceBox[] faces) =>
        new() { AssetId = "x", ImageWidth = width, ImageHeight = height, Faces = faces };

    [Fact]
    public void NoFaces_IsDefault() => Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(0, 0)));

    [Fact]
    public void FaceUnderTopRightChips_IsMirrored() =>
        Assert.Equal(OverlayLayout.Mirrored, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.75, 0.10, 0.90, 0.35))));

    [Fact]
    public void FaceWhereMirroredPanelWouldSit_IsDefault() =>
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.60, 0.72, 0.90, 0.95))));

    [Fact]
    public void CenteredFace_IsDefault() =>
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(1024, 600, new FaceBox(0.45, 0.40, 0.55, 0.60))));

    [Fact]
    public void FaceCroppedOffScreen_IsIgnored()
    {
        // Portrait 600x1200 shown with object-fit: cover at 1024x600 keeps only the middle ~29% of
        // its height, so a face at the very top is cropped away and must not push the overlays aside.
        Assert.Equal(OverlayLayout.Default, OverlayLayoutChooser.Choose(photo(600, 1200, new FaceBox(0.75, 0.05, 0.90, 0.15))));
    }
}
