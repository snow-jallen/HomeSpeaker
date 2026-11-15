using HomeSpeaker.Server2.Services.YouTube;

namespace HomeSpeaker.Server2.Tests.YouTube;

public sealed class YouTubeLinkTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc123")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ&t=42s")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
    public void VideoLinksAreOneVideo(string text)
    {
        var link = YouTubeLink.TryParse(text);

        Assert.Equal(new YouTubeLink("dQw4w9WgXcQ", null), link);
        Assert.False(link!.IsPlaylist);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLOU2XLYxmsIIM9h1Ybw2DuRw6o2fkNMeR")]
    [InlineData("https://music.youtube.com/playlist?list=PLOU2XLYxmsIIM9h1Ybw2DuRw6o2fkNMeR")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLOU2XLYxmsIIM9h1Ybw2DuRw6o2fkNMeR&index=3")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?list=PLOU2XLYxmsIIM9h1Ybw2DuRw6o2fkNMeR")]
    public void PlaylistLinksAreTheWholePlaylist(string text)
    {
        var link = YouTubeLink.TryParse(text);

        Assert.Equal(new YouTubeLink(null, "PLOU2XLYxmsIIM9h1Ybw2DuRw6o2fkNMeR"), link);
        Assert.True(link!.IsPlaylist);
    }

    [Fact]
    public void AVideoPlayingInAGeneratedMixIsJustTheVideo()
    {
        var link = YouTubeLink.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ&start_radio=1");

        Assert.Equal(new YouTubeLink("dQw4w9WgXcQ", null), link);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hymns of praise")]
    [InlineData("dQw4w9WgXcQ")] // a bare id is a search term, not a link
    [InlineData("https://vimeo.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/@SomeChannel")]
    [InlineData("ftp://youtube.com/watch?v=dQw4w9WgXcQ")]
    public void AnythingElseIsNotALink(string? text)
    {
        Assert.Null(YouTubeLink.TryParse(text));
    }
}
