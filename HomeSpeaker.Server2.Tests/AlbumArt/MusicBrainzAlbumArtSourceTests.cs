using System.Net;
using HomeSpeaker.Server2.Services.AlbumArt;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class MusicBrainzAlbumArtSourceTests
{
    private const string ReleaseJson = """
        {
          "created": "2026-10-29T00:00:00.000Z", "count": 3, "offset": 0,
          "releases": [
            {
              "id": "5b1d0f3a-0000-4000-8000-000000000001", "score": 100, "title": "A Night at the Opera",
              "artist-credit": [ { "name": "Queen", "artist": { "id": "0383dadf-2a4e-4d10-a46a-e9e041da8eb3", "name": "Queen" } } ],
              "release-group": { "id": "e8ee0a6d-0000-4000-8000-0000000000aa", "primary-type": "Album" }
            },
            {
              "id": "5b1d0f3a-0000-4000-8000-000000000002", "score": 98, "title": "A Night at the Opera",
              "artist-credit": [ { "name": "Queen" } ],
              "release-group": { "id": "e8ee0a6d-0000-4000-8000-0000000000aa" }
            },
            {
              "id": "5b1d0f3a-0000-4000-8000-000000000003", "score": 60, "title": "Bridge over Troubled Water",
              "artist-credit": [ { "name": "Simon", "joinphrase": " & " }, { "name": "Garfunkel" } ],
              "release-group": { "id": "e8ee0a6d-0000-4000-8000-0000000000bb" }
            }
          ]
        }
        """;

    private const string RecordingJson = """
        {
          "count": 1,
          "recordings": [
            {
              "id": "b1a9c0e9-0000-4000-8000-000000000010", "title": "Bohemian Rhapsody",
              "artist-credit": [ { "name": "Queen" } ],
              "releases": [
                { "id": "r1", "title": "A Night at the Opera", "release-group": { "id": "e8ee0a6d-0000-4000-8000-0000000000aa" } },
                { "id": "r2", "title": "Greatest Hits", "release-group": { "id": "e8ee0a6d-0000-4000-8000-0000000000cc" } },
                { "id": "r3", "title": "No group" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void BuildUri_Album_SearchesReleasesByReleaseAndArtist()
    {
        var uri = MusicBrainzAlbumArtSource.BuildUri(new AlbumArtQuery("Queen", "A Night at the \"Opera\"", null));

        Assert.Equal("https://musicbrainz.org/ws/2/release/", uri.GetLeftPart(UriPartial.Path));
        Assert.Equal(
            "?query=" + Uri.EscapeDataString("release:\"A Night at the  Opera\" AND artist:\"Queen\"") + "&fmt=json&limit=10",
            uri.Query);
    }

    [Fact]
    public void BuildUri_SongWithoutArtist_SearchesRecordingsByTitle()
    {
        var uri = MusicBrainzAlbumArtSource.BuildUri(new AlbumArtQuery(null, null, "Bohemian Rhapsody"));

        Assert.Equal("https://musicbrainz.org/ws/2/recording/?query=" + Uri.EscapeDataString("recording:\"Bohemian Rhapsody\"") + "&fmt=json&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildUri_FreeText_SearchesReleasesWithDismax()
    {
        var uri = MusicBrainzAlbumArtSource.BuildUri(AlbumArtQuery.FreeText("queen \"night\" opera"));

        Assert.Equal("https://musicbrainz.org/ws/2/release/?query=" + Uri.EscapeDataString("queen \"night\" opera") + "&dismax=true&fmt=json&limit=10", uri.AbsoluteUri);
    }

    [Fact]
    public async Task SearchAsync_FreeText_ReadsReleases()
    {
        var (factory, _) = AlbumArtTestHttp.Json(ReleaseJson);
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(AlbumArtQuery.FreeText("queen night at the opera"), CancellationToken.None);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate => Assert.Null(candidate.Title));
    }

    [Fact]
    public async Task SearchAsync_Releases_OneCandidatePerReleaseGroupFromCoverArtArchive()
    {
        var (factory, _) = AlbumArtTestHttp.Json(ReleaseJson);
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", "A Night at the Opera", null), CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("MusicBrainz", candidates[0].Source);
        Assert.Equal("https://coverartarchive.org/release-group/e8ee0a6d-0000-4000-8000-0000000000aa/front-500", candidates[0].ImageUrl);
        Assert.Equal("https://coverartarchive.org/release-group/e8ee0a6d-0000-4000-8000-0000000000aa/front-250", candidates[0].ThumbnailUrl);
        Assert.Equal("Queen", candidates[0].Artist);
        Assert.Equal("A Night at the Opera", candidates[0].Album);
        Assert.Equal("Simon & Garfunkel", candidates[1].Artist);
    }

    [Fact]
    public async Task SearchAsync_Recordings_UseEachReleaseGroupOnce()
    {
        var (factory, _) = AlbumArtTestHttp.Json(RecordingJson);
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", null, "Bohemian Rhapsody"), CancellationToken.None);

        Assert.Equal(["A Night at the Opera", "Greatest Hits"], candidates.Select(c => c.Album));
        Assert.All(candidates, c => Assert.Equal("Bohemian Rhapsody", c.Title));
        Assert.All(candidates, c => Assert.Equal("Queen", c.Artist));
    }

    [Fact]
    public async Task SearchAsync_ServiceUnavailableTwice_Throws()
    {
        var (factory, handler) = AlbumArtTestHttp.Json("""{"error":"rate limited"}""", HttpStatusCode.ServiceUnavailable);
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchAsync(new AlbumArtQuery("Queen", "Jazz", null), CancellationToken.None));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_ServiceUnavailableOnce_RetriesAfterThrottle()
    {
        var calls = 0;
        var (factory, handler) = AlbumArtTestHttp.Respond(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Photos.FakeHttpMessageHandler.Json(ReleaseJson));
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        var candidates = await source.SearchAsync(new AlbumArtQuery("Queen", "A Night at the Opera", null), CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_OtherHttpError_DoesNotRetry()
    {
        var (factory, handler) = AlbumArtTestHttp.Json("{}", HttpStatusCode.BadRequest);
        using var source = new MusicBrainzAlbumArtSource(factory, TimeProvider.System);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.SearchAsync(new AlbumArtQuery("Queen", "Jazz", null), CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void LookupClient_SendsDescriptiveUserAgent()
    {
        using var factory = new TestServerFactory();
        var clientFactory = factory.Services.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient(AlbumArtHttp.LookupClientName);

        var userAgent = client.DefaultRequestHeaders.UserAgent.ToString();

        Assert.Contains("HomeSpeaker/1.0", userAgent, StringComparison.Ordinal);
        Assert.Contains("https://github.com/snow-jallen/homespeaker", userAgent, StringComparison.Ordinal);
    }
}
