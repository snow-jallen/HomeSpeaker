using HomeSpeaker.Server2.Services.AlbumArt;
using HomeSpeaker.Server2.Tests.Photos;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class AlbumArtImageTests
{
    [Fact]
    public async Task FromUploadAsync_Jpeg_IsAccepted()
    {
        var jpeg = TestJpeg.Header(300, 300);
        using var stream = new MemoryStream(jpeg);

        var image = await AlbumArtImage.FromUploadAsync(stream, CancellationToken.None);

        Assert.Equal("image/jpeg", image.MimeType);
        Assert.Equal(jpeg, image.Bytes);
    }

    [Fact]
    public async Task FromUploadAsync_Gif_IsRejected()
    {
        // The page converts to JPEG in the browser; anything else reaching the server is refused, not embedded.
        using var stream = new MemoryStream("GIF89a\u0001\u0000\u0001\u0000"u8.ToArray());

        await Assert.ThrowsAsync<AlbumArtDownloadException>(() => AlbumArtImage.FromUploadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task FromUploadAsync_OverTheCap_IsRejected()
    {
        var big = new byte[AlbumArtDownloader.MaxBytes + 1];
        TestJpeg.Header(10, 10).CopyTo(big, 0);
        using var stream = new MemoryStream(big);

        var ex = await Assert.ThrowsAsync<AlbumArtDownloadException>(() => AlbumArtImage.FromUploadAsync(stream, CancellationToken.None));
        Assert.Contains("MB", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDataUrl_EncodesTheBytes()
    {
        var image = new AlbumArtImage([0xFF, 0xD8, 0xFF], "image/jpeg");

        Assert.Equal("data:image/jpeg;base64,/9j/", image.ToDataUrl());
    }
}
