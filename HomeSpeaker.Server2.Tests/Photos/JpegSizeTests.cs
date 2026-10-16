using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class JpegSizeTests
{
    [Theory]
    [InlineData(0xC0)]
    [InlineData(0xC2)]
    public void TryRead_ReadsTheFrameHeaderAfterOtherSegments(byte frameMarker)
    {
        using var stream = new MemoryStream(TestJpeg.Header(1440, 1920, frameMarker));

        Assert.True(JpegSize.TryRead(stream, out var width, out var height));
        Assert.Equal(1440, width);
        Assert.Equal(1920, height);
    }

    [Fact]
    public void TryRead_SkipsHuffmanTablesThatShareTheFrameMarkerRange()
    {
        byte[] dht = [0xFF, 0xC4, 0x00, 0x04, 0x00, 0x00];
        var bytes = TestJpeg.Header(800, 600);
        using var stream = new MemoryStream([.. bytes[..2], .. dht, .. bytes[2..]]);

        Assert.True(JpegSize.TryRead(stream, out var width, out var height));
        Assert.Equal((800, 600), (width, height));
    }

    [Theory]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x40 })]
    public void TryRead_RejectsAnythingWithoutAFrameHeader(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        Assert.False(JpegSize.TryRead(stream, out _, out _));
    }
}
