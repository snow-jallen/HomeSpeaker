using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Tests;

public sealed class StreamUrlTests
{
    [Theory]
    [InlineData("http://example.com/kbaq")]
    [InlineData("https://example.com:8443/live.mp3?x=1")]
    public void HttpAndHttpsUrlsAreAccepted(string url)
    {
        Assert.NotNull(StreamUrl.Normalize(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/music/song.mp3")]
    [InlineData("file:///certs/certificate.pfx")]
    [InlineData("ftp://example.com/a.mp3")]
    [InlineData("rtsp://example.com/a")]
    public void OtherUrlsAreRejected(string? url)
    {
        Assert.Null(StreamUrl.Normalize(url));
    }

    [Fact]
    public void QuotesAndSpacesAreEscapedSoTheyCannotEndTheArgument()
    {
        var normalized = StreamUrl.Normalize("http://evil.example/a\" --sout=#file{dst=/music/x} \"");

        Assert.NotNull(normalized);
        Assert.DoesNotContain('"', normalized);
        Assert.DoesNotContain(' ', normalized);
    }
}
