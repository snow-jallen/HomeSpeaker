using HomeSpeaker.Server2.Services.AlbumArt;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class DeezerAlbumArtSourceTests
{
    private const string AlbumJson = """
        {
          "data": [
            {
              "id": 1121182, "title": "A Night At The Opera (2011 Remaster)",
              "cover": "https://api.deezer.com/album/1121182/image",
              "cover_medium": "https://e-cdns-images.dzcdn.net/images/cover/abc/250x250-000000-80-0-0.jpg",
              "cover_big": "https://e-cdns-images.dzcdn.net/images/cover/abc/500x500-000000-80-0-0.jpg",
              "cover_xl": "https://e-cdns-images.dzcdn.net/images/cover/abc/1000x1000-000000-80-0-0.jpg",
              "artist": { "id": 412, "name": "Queen" }, "type": "album"
            },
            { "id": 2, "title": "No cover", "artist": { "name": "Queen" } }
          ],
          "total": 2
        }
        """;

    private const string TrackJson = """
        {
          "data": [
            {
              "id": 9997018, "title": "Bohemian Rhapsody",
              "artist": { "id": 412, "name": "Queen" },
              "album": {
                "id": 1121182, "title": "A Night At The Opera",
                "cover_medium": "https://e-cdns-images.dzcdn.net/images/cover/abc/250x250.jpg",
                "cover_xl": "https://e-cdns-images.dzcdn.net/images/cover/abc/1000x1000.jpg"
              }
            }
          ],
          "total": 1
        }
        """;

    [Fact]
    public void BuildUri_Album_SearchesAlbumsByArtistAndAlbumText()
    {
        var uri = DeezerAlbumArtSource.BuildUri(new AlbumArtQuery("Queen", "A Night at the Opera", null));

        Assert.Equal("https://api.deezer.com/search/album?q=Queen%20A%20Night%20at%20the%20Opera&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildUri_SongWithoutArtist_UsesTrackSearch()
    {
        var uri = DeezerAlbumArtSource.BuildUri(new AlbumArtQuery(null, null, "Say \"Hello\""));

        Assert.Equal("https://api.deezer.com/search/track?q=" + Uri.EscapeDataString("Say \"Hello\"") + "&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildUri_FreeText_SearchesAlbumsForTheWords()
    {
        var uri = DeezerAlbumArtSource.BuildUri(AlbumArtQuery.FreeText("queen night opera"));

        Assert.Equal("https://api.deezer.com/search/album?q=queen%20night%20opera&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public async Task SearchAsync_Albums_UseCoverXl()
    {
        var (factory, _) = AlbumArtTestHttp.Json(AlbumJson);
        using var source = new DeezerAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", "A Night at the Opera", null), CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Deezer", candidate.Source);
        Assert.Equal("https://e-cdns-images.dzcdn.net/images/cover/abc/1000x1000-000000-80-0-0.jpg", candidate.ImageUrl);
        Assert.Equal("https://e-cdns-images.dzcdn.net/images/cover/abc/250x250-000000-80-0-0.jpg", candidate.ThumbnailUrl);
        Assert.Equal("Queen", candidate.Artist);
        Assert.Equal("A Night At The Opera (2011 Remaster)", candidate.Album);
    }

    [Fact]
    public async Task SearchAsync_Tracks_UseTheTrackAlbumCover()
    {
        var (factory, _) = AlbumArtTestHttp.Json(TrackJson);
        using var source = new DeezerAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", null, "Bohemian Rhapsody"), CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("https://e-cdns-images.dzcdn.net/images/cover/abc/1000x1000.jpg", candidate.ImageUrl);
        Assert.Equal("A Night At The Opera", candidate.Album);
        Assert.Equal("Bohemian Rhapsody", candidate.Title);
    }

    [Fact]
    public async Task SearchAsync_ErrorObject_Throws()
    {
        var (factory, _) = AlbumArtTestHttp.Json("""{"error":{"type":"Exception","message":"Quota limit exceeded","code":4}}""");
        using var source = new DeezerAlbumArtSource(factory, TimeProvider.System);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchAsync(new AlbumArtQuery("Queen", "Jazz", null), CancellationToken.None));
        Assert.Contains("Quota limit exceeded", ex.Message, StringComparison.Ordinal);
    }
}
