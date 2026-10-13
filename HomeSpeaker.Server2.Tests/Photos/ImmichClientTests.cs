using System.Net;
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichClientTests
{
    private static (ImmichClient Client, FakeHttpMessageHandler Handler) create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://immich.test/") };
        return (new ImmichClient(http), handler);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_FollowsNextPage_AndSkipsTrashedAndVideos()
    {
        var (client, handler) = create((_, body) => body!.Contains("\"page\":1", StringComparison.Ordinal)
            ? FakeHttpMessageHandler.Json("""
                {"assets":{"items":[
                  {"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":{"city":"Garden City","state":"Utah","country":"United States of America"}},
                  {"id":"a2","type":"IMAGE","isTrashed":true,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":null}
                ],"nextPage":"2"}}
                """)
            : FakeHttpMessageHandler.Json("""
                {"assets":{"items":[
                  {"id":"a3","type":"IMAGE","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":{"city":null,"state":"","country":"Mexico"}},
                  {"id":"a4","type":"VIDEO","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null}
                ],"nextPage":null}}
                """));

        var assets = await client.GetAlbumImagesAsync("album-1", CancellationToken.None);

        Assert.Equal(["a1", "a3"], assets.Select(a => a.Id));
        Assert.Equal("Garden City", assets[0].City);
        Assert.Equal("Utah", assets[0].State);
        Assert.Null(assets[1].City);
        Assert.Null(assets[1].State); // blank strings normalize to null
        Assert.Equal("Mexico", assets[1].Country);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Request.Method);
            Assert.Equal("/api/search/metadata", r.Request.RequestUri!.AbsolutePath);
            Assert.Contains("\"albumIds\":[\"album-1\"]", r.Body, StringComparison.Ordinal);
            Assert.Contains("\"type\":\"IMAGE\"", r.Body, StringComparison.Ordinal);
            Assert.Contains("\"withExif\":true", r.Body, StringComparison.Ordinal);
        });
        Assert.Contains("\"page\":2", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_KeepsLocalWallClockTime()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumImagesAsync("album-1", CancellationToken.None));

        Assert.Equal(new DateTime(2021, 8, 14, 13, 22, 5), asset.TakenAt);
        Assert.Equal(DateTimeKind.Unspecified, asset.TakenAt.Kind);
    }

    [Fact]
    public async Task GetAlbumImagesAsync_WhenUnauthorized_Throws()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumImagesAsync("album-1", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadPreviewAsync_RequestsPreviewSize_AndCopiesBytes()
    {
        var (client, handler) = create((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var destination = new MemoryStream();

        await client.DownloadPreviewAsync("a1", destination, CancellationToken.None);

        Assert.Equal([1, 2, 3], destination.ToArray());
        Assert.Equal("/api/assets/a1/thumbnail", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("?size=preview", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetFacesAsync_MapsBoundingBoxes()
    {
        var (client, handler) = create((_, _) => FakeHttpMessageHandler.Json("""
            [{"id":"f1","boundingBoxX1":100,"boundingBoxY1":50,"boundingBoxX2":300,"boundingBoxY2":250,"imageWidth":1000,"imageHeight":500,"person":null,"sourceType":"machine-learning"}]
            """));

        var face = Assert.Single(await client.GetFacesAsync("a1", CancellationToken.None));

        Assert.Equal(new ImmichFace(100, 50, 300, 250, 1000, 500), face);
        Assert.Equal("/api/faces", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("?id=a1", handler.Requests[0].Request.RequestUri!.Query);
    }
}
