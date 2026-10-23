using System.Net;
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichAlbumBrowserTests
{
    [Fact]
    public async Task GetAlbums_SendsApiKeyAndParsesAlbums()
    {
        var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json("""
            [
              {"id":"a-1","albumName":"Family","assetCount":120,"shared":false},
              {"id":"a-2","albumName":"Trips","assetCount":7}
            ]
            """));
        var factory = new FakeHttpClientFactory(new HttpClient(handler));
        var browser = new ImmichAlbumBrowser(factory);

        var albums = await browser.GetAlbumsAsync("https://immich.test/", "key-123", CancellationToken.None);

        Assert.Equal([new ImmichAlbum("a-1", "Family", 120), new ImmichAlbum("a-2", "Trips", 7)], albums);
        Assert.Equal(ImmichAlbumBrowser.ClientName, factory.LastName);
        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal("https://immich.test/api/albums", request.RequestUri!.AbsoluteUri);
        Assert.Equal(["key-123"], request.Headers.GetValues("x-api-key"));
    }

    [Fact]
    public async Task GetAlbums_Unauthorized_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json("""{"message":"Invalid API key"}""", HttpStatusCode.Unauthorized));
        var browser = new ImmichAlbumBrowser(new FakeHttpClientFactory(new HttpClient(handler)));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => browser.GetAlbumsAsync("https://immich.test", "bad", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetAlbums_InvalidUrl_ThrowsArgumentException()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException("no HTTP expected"));
        var browser = new ImmichAlbumBrowser(new FakeHttpClientFactory(new HttpClient(handler)));

        await Assert.ThrowsAsync<ArgumentException>(() => browser.GetAlbumsAsync("ftp://immich.test", "key", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }
}
