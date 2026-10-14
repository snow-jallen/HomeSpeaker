using System.Net;
using HomeSpeaker.Server2.Endpoints;
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoEndpointsVideoTests : IDisposable
{
    private const string VideoId = "11111111-1111-4111-8111-111111111111";
    private static readonly TimeSpan longTimeout = TimeSpan.FromSeconds(30);
    private readonly TempDirectory temp = new();
    private readonly FakeImmichClient immich = new();
    private readonly PhotoCacheStore store;
    private readonly VideoStreamCoordinator coordinator = new();
    private readonly PhotoLibrary library;

    public PhotoEndpointsVideoTests()
    {
        store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);
        store.SaveManifest([new PhotoEntry { AssetId = VideoId, TakenAt = new DateTime(2021, 8, 14), IsVideo = true, DurationMs = 5000 }]);
        library = new PhotoLibrary(store, new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)), new Random(1));
    }

    public void Dispose() => temp.Dispose();

    private static void assertStatus(int expected, IResult result) =>
        Assert.Equal(expected, Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result).StatusCode);

    private Task<IResult> stream(HttpContext context, TimeSpan? headerTimeout = null) =>
        PhotoEndpoints.StreamVideoAsync(VideoId, library, store, coordinator, immich, context, NullLogger<PhotoLibrary>.Instance, headerTimeout ?? longTimeout);

    private async Task<byte[]> saveCachedVideoAsync(byte[] bytes)
    {
        await store.SaveVideoAsync(VideoId, ".mp4", s => s.WriteAsync(bytes).AsTask());
        return bytes;
    }

    private static DefaultHttpContext rangeContext(string? range, MemoryStream body)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        if (range is not null)
        {
            context.Request.Headers.Range = range;
        }

        context.Response.Body = body;
        return context;
    }

    [Fact]
    public async Task Stream_WhenVideoIsCached_ServesTheFileWithRangeSupportAndNeverCallsImmich()
    {
        var bytes = await saveCachedVideoAsync(Enumerable.Range(0, 200).Select(i => (byte)i).ToArray());
        using var body = new MemoryStream();
        var context = rangeContext("bytes=0-99", body);

        var result = await stream(context);
        await result.ExecuteAsync(context);

        var file = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.FileStreamHttpResult>(result);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("video/mp4", file.ContentType);
        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal("bytes 0-99/200", context.Response.Headers.ContentRange.ToString());
        Assert.Equal(bytes.Take(100), body.ToArray());
        Assert.Empty(immich.OpenedVideoIds);
    }

    [Fact]
    public async Task Stream_WhenTheCachedFileCannotBeOpened_FallsBackToTheProxyInsteadOfFailing()
    {
        await saveCachedVideoAsync([1, 2, 3]);
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 9]) });
        using var body = new MemoryStream();

        // Only Windows can hold a file open exclusively; elsewhere the delete race is covered by the store test.
        using var exclusive = OperatingSystem.IsWindows()
            ? new FileStream(Path.Combine(store.VideosPath, VideoId + ".mp4"), FileMode.Open, FileAccess.Read, FileShare.None)
            : null;
        var result = await stream(rangeContext(null, body));

        if (OperatingSystem.IsWindows())
        {
            Assert.Same(Results.Empty, result);
            Assert.Equal([VideoId], immich.OpenedVideoIds);
        }
    }

    [Fact]
    public async Task Stream_WhenTheFileVanishedAfterTheCheck_FallsBackToTheProxy()
    {
        // No cached file at all: the same path the endpoint takes when an eviction wins the race.
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 9]) });
        using var body = new MemoryStream();

        var result = await stream(rangeContext(null, body));

        Assert.Same(Results.Empty, result);
        Assert.Equal(new byte[] { 9, 9 }, body.ToArray());
    }

    [Fact]
    public async Task Stream_WhileProxying_RegistersTheVideoAsLiveAndReleasesItAfterwards()
    {
        var wasLive = false;
        immich.OpenVideo = (_, _) =>
        {
            wasLive = coordinator.IsLive(VideoId);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        };
        using var body = new MemoryStream();

        await stream(rangeContext(null, body));

        Assert.True(wasLive);
        Assert.False(coordinator.IsLive(VideoId));
    }

    [Fact]
    public async Task Stream_WhenOnlyAPartialDownloadExists_FallsBackToTheProxy()
    {
        Directory.CreateDirectory(store.VideosPath);
        await File.WriteAllBytesAsync(Path.Combine(store.VideosPath, VideoId + ".mp4.tmp"), [1, 2, 3]);
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 9]) });
        using var body = new MemoryStream();

        var result = await stream(rangeContext(null, body));

        Assert.Same(Results.Empty, result);
        Assert.Equal(new byte[] { 9, 9 }, body.ToArray());
        Assert.Equal([VideoId], immich.OpenedVideoIds);
    }

    [Fact]
    public async Task Stream_WhenImmichNeverSendsHeaders_TimesOutWith502()
    {
        immich.OpenVideo = async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var result = await stream(new DefaultHttpContext(), TimeSpan.FromMilliseconds(50));

        assertStatus(StatusCodes.Status502BadGateway, result);
    }

    [Fact]
    public async Task Stream_WhenBrowserAbortsWhileWaitingForHeaders_ReturnsEmpty()
    {
        using var aborted = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        immich.OpenVideo = async (_, ct) =>
        {
            await aborted.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var result = await stream(context);

        Assert.Same(Results.Empty, result);
    }

    [Fact]
    public async Task Stream_HeaderTimeoutDoesNotLimitTheBodyStream()
    {
        byte[] bytes = [1, 2, 3];
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new SlowStream(bytes, TimeSpan.FromMilliseconds(400))) });
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        var result = await stream(context, TimeSpan.FromMilliseconds(200));

        Assert.Same(Results.Empty, result);
        Assert.Equal(bytes, body.ToArray());
    }

    [Fact]
    public async Task Stream_WhenUpstreamFailsBeforeResponseStarts_Returns502()
    {
        immich.OpenVideo = (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused"));

        assertStatus(StatusCodes.Status502BadGateway, await stream(new DefaultHttpContext()));
    }

    [Fact]
    public async Task Stream_WhenUpstreamIsNotSuccessful_Returns502()
    {
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        assertStatus(StatusCodes.Status502BadGateway, await stream(new DefaultHttpContext()));
    }

    [Fact]
    public async Task Stream_WhenUpstreamDropsAfterResponseStarted_ReturnsEmptyInsteadOf502()
    {
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FailingStream()) });
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        context.Response.Body = new MemoryStream();

        var result = await stream(context);

        Assert.Same(Results.Empty, result);
    }

    [Fact]
    public async Task Stream_ForUnknownOrInvalidId_Returns404()
    {
        var context = new DefaultHttpContext();

        var unknown = await PhotoEndpoints.StreamVideoAsync(
            "22222222-2222-4222-8222-222222222222", library, store, coordinator, immich, context, NullLogger<PhotoLibrary>.Instance, longTimeout);
        var invalid = await PhotoEndpoints.StreamVideoAsync("../x", library, store, coordinator, immich, context, NullLogger<PhotoLibrary>.Instance, longTimeout);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(unknown);
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(invalid);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    /// <summary>Delivers its bytes only after a delay, i.e. well after the header timeout would have fired.</summary>
    private sealed class SlowStream(byte[] bytes, TimeSpan delay) : Stream
    {
        private bool done;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (done)
            {
                return 0;
            }

            await Task.Delay(delay, cancellationToken);
            done = true;
            bytes.CopyTo(buffer);
            return bytes.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("upstream connection dropped mid-body");

        public override int Read(byte[] buffer, int offset, int count) => throw new HttpRequestException("upstream connection dropped mid-body");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
