using System.Net;
using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichClientTests
{
    private static (ImmichClient Client, FakeHttpMessageHandler Handler) create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://immich.test/") };
        return (new ImmichClient(http, new FakeHttpClientFactory(http)), handler);
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_FollowsNextPage_KeepsVideos_AndSkipsTrashedAndOtherTypes()
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
                  {"id":"a4","type":"VIDEO","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null},
                  {"id":"a5","type":"AUDIO","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null},
                  {"id":"a6","type":"OTHER","isTrashed":false,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null},
                  {"id":"a7","type":"VIDEO","isTrashed":true,"localDateTime":"2019-06-01T08:00:00.000Z","exifInfo":null}
                ],"nextPage":null}}
                """));

        var assets = await client.GetAlbumAssetsAsync("album-1", CancellationToken.None);

        Assert.Equal(["a1", "a3", "a4"], assets.Select(a => a.Id));
        Assert.Equal([false, false, true], assets.Select(a => a.IsVideo));
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
            Assert.DoesNotContain("\"type\"", r.Body, StringComparison.Ordinal);
            Assert.Contains("\"withExif\":true", r.Body, StringComparison.Ordinal);
        });
        Assert.Contains("\"page\":2", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_KeepsLocalWallClockTime()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumAssetsAsync("album-1", CancellationToken.None));

        Assert.Equal(new DateTime(2021, 8, 14, 13, 22, 5), asset.TakenAt);
        Assert.Equal(DateTimeKind.Unspecified, asset.TakenAt.Kind);
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_WhenUnauthorized_Throws()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("{}", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAlbumAssetsAsync("album-1", CancellationToken.None));
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_ParsesVideoDurationFromNumber()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"v1","type":"VIDEO","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","duration":12345,"exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumAssetsAsync("album-1", CancellationToken.None));

        Assert.True(asset.IsVideo);
        Assert.Equal(12345, asset.DurationMs);
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_ParsesVideoDurationFromLegacyString()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"v1","type":"VIDEO","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","duration":"0:00:12.345000","exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumAssetsAsync("album-1", CancellationToken.None));

        Assert.Equal(12345, asset.DurationMs);
    }

    [Fact]
    public async Task GetAlbumAssetsAsync_ImagesHaveNoDuration()
    {
        var (client, _) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"assets":{"items":[{"id":"a1","type":"IMAGE","isTrashed":false,"localDateTime":"2021-08-14T13:22:05.000Z","duration":"0:00:00.000000","exifInfo":null}],"nextPage":null}}
            """));

        var asset = Assert.Single(await client.GetAlbumAssetsAsync("album-1", CancellationToken.None));

        Assert.False(asset.IsVideo);
        Assert.Null(asset.DurationMs);
    }

    [Fact]
    public async Task OpenVideoAsync_ForwardsRange_AndReturnsResponseUnchanged()
    {
        var (client, handler) = create((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new("video/mp4");
            return response;
        });

        using var response = await client.OpenVideoAsync("a1", "bytes=0-", CancellationToken.None);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
        var request = handler.Requests[0].Request;
        Assert.Equal("/api/assets/a1/video/playback", request.RequestUri!.AbsolutePath);
        Assert.Equal("bytes=0-", Assert.Single(request.Headers.GetValues("Range")));
    }

    [Fact]
    public async Task OpenVideoAsync_WithoutRange_SendsNoRangeHeader_AndDoesNotThrowOnFailure()
    {
        var (client, handler) = create((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var response = await client.OpenVideoAsync("a1", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(handler.Requests[0].Request.Headers.Contains("Range"));
    }

    [Fact]
    public async Task OpenVideoAsync_UsesTheNamedImmichVideoClient()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://immich.test/") };
        var factory = new FakeHttpClientFactory(http);
        using var unused = new HttpClient(new FakeHttpMessageHandler((_, _) => throw new InvalidOperationException()));
        var client = new ImmichClient(unused, factory);

        using var response = await client.OpenVideoAsync("a1", null, CancellationToken.None);

        Assert.Equal("ImmichVideo", factory.LastName);
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
