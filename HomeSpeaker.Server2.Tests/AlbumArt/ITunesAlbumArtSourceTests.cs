using System.Net;
using HomeSpeaker.Server2.Services.AlbumArt;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class ITunesAlbumArtSourceTests
{
    private const string AlbumJson = """
        {
          "resultCount": 2,
          "results": [
            {
              "wrapperType": "collection", "collectionType": "Album", "artistId": 3296287, "collectionId": 1440650428,
              "artistName": "Queen", "collectionName": "A Night At the Opera (2011 Remaster)",
              "artworkUrl60": "https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/aa/bb/cc/source/60x60bb.jpg",
              "artworkUrl100": "https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/aa/bb/cc/source/100x100bb.jpg",
              "trackCount": 12, "primaryGenreName": "Rock"
            },
            { "wrapperType": "collection", "artistName": "Queen", "collectionName": "No Art" }
          ]
        }
        """;

    private const string SongJson = """
        {
          "resultCount": 1,
          "results": [
            {
              "wrapperType": "track", "kind": "song", "artistName": "Queen", "collectionName": "A Night At the Opera",
              "trackName": "Bohemian Rhapsody",
              "artworkUrl100": "https://is1-ssl.mzstatic.com/image/thumb/Music/x/source/100x100bb.png"
            }
          ]
        }
        """;

    [Fact]
    public void BuildUri_Album_SearchesAlbumEntityWithArtistAndAlbum()
    {
        var uri = ITunesAlbumArtSource.BuildUri(new AlbumArtQuery("Queen", "A Night at the Opera", null));

        Assert.Equal("https://itunes.apple.com/search?term=Queen%20A%20Night%20at%20the%20Opera&media=music&entity=album&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildUri_SongWithoutArtist_SearchesSongEntityByTitle()
    {
        var uri = ITunesAlbumArtSource.BuildUri(new AlbumArtQuery(null, null, "Bohemian Rhapsody"));

        Assert.Equal("https://itunes.apple.com/search?term=Bohemian%20Rhapsody&media=music&entity=song&limit=10", uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/a/source/100x100bb.jpg", "https://is1-ssl.mzstatic.com/image/thumb/a/source/600x600bb.jpg")]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/a/source/60x60bb.png", "https://is1-ssl.mzstatic.com/image/thumb/a/source/600x600bb.png")]
    [InlineData("https://example.com/cover.jpg", "https://example.com/cover.jpg")]
    public void Resize_RewritesSizeSegment(string original, string expected)
    {
        Assert.Equal(expected, ITunesAlbumArtSource.Resize(original, 600));
    }

    [Fact]
    public async Task SearchAsync_AlbumResults_ReturnsUpscaledArtworkAndNames()
    {
        var (factory, handler) = AlbumArtTestHttp.Json(AlbumJson);
        using var source = new ITunesAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", "A Night at the Opera", null), CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("iTunes", candidate.Source);
        Assert.Equal("https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/aa/bb/cc/source/600x600bb.jpg", candidate.ImageUrl);
        Assert.Equal("https://is1-ssl.mzstatic.com/image/thumb/Music125/v4/aa/bb/cc/source/200x200bb.jpg", candidate.ThumbnailUrl);
        Assert.Equal("Queen", candidate.Artist);
        Assert.Equal("A Night At the Opera (2011 Remaster)", candidate.Album);
        Assert.Null(candidate.Title);
        Assert.Equal(AlbumArtHttp.LookupClientName, factory.LastName);
        Assert.Contains("entity=album", handler.Requests[0].Request.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_SongResults_IncludeTrackName()
    {
        var (factory, _) = AlbumArtTestHttp.Json(SongJson);
        using var source = new ITunesAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", null, "Bohemian Rhapsody"), CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Bohemian Rhapsody", candidate.Title);
        Assert.Equal("A Night At the Opera", candidate.Album);
        Assert.EndsWith("/600x600bb.png", candidate.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ServerError_Throws()
    {
        var (factory, _) = AlbumArtTestHttp.Json("{}", HttpStatusCode.Forbidden);
        using var source = new ITunesAlbumArtSource(factory, TimeProvider.System);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchAsync(new AlbumArtQuery("Queen", "Jazz", null), CancellationToken.None));
    }
}
