using System.Net;
using System.Net.Http.Headers;
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class VideoPrefetcherTests : IDisposable
{
    private const long MB = 1024 * 1024;
    private readonly TempDirectory temp = new();
    private readonly PhotoCacheStore store;
    private readonly FakeImmichClient immich = new();
    private readonly VideoStreamCoordinator coordinator = new();
    private readonly CapturingLogger<VideoPrefetcher> log = new();
    private readonly ImmichOptions options = new()
    {
        BaseUrl = "https://immich.test",
        ApiKey = "key",
        AlbumId = "album",
        VideoPrefetchAhead = 3,
        VideoPrefetchMaxMB = 1,
    };

    private PhotoLibrary? library;

    public VideoPrefetcherTests()
    {
        store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4([1, 2, 3, 4]));
    }

    public void Dispose() => temp.Dispose();

    private static string idFor(int n) => $"{n:x8}-0000-4000-8000-000000000000";

    private static HttpResponseMessage mp4(byte[] bytes, string contentType = "video/mp4")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private async Task seedAsync(int videos, int images = 0)
    {
        var entries = new List<PhotoEntry>();
        for (var i = 0; i < images; i++)
        {
            await store.SaveImageAsync(idFor(1000 + i), s => s.WriteAsync(new byte[] { 1 }).AsTask());
            entries.Add(new PhotoEntry { AssetId = idFor(1000 + i), TakenAt = new DateTime(2021, 8, 14) });
        }

        for (var i = 0; i < videos; i++)
        {
            entries.Add(new PhotoEntry { AssetId = idFor(i), TakenAt = new DateTime(2021, 8, 14), IsVideo = true, DurationMs = 5000 });
        }

        store.SaveManifest(entries);
        library = new PhotoLibrary(store, new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)), new Random(3));
    }

    private VideoPrefetcher createPrefetcher(TimeSpan? headerTimeout = null, TimeSpan? inactivityTimeout = null, Func<long?>? freeSpace = null) =>
        new(library!, store, immich, Options.Create(options), TimeProvider.System, coordinator, log)
        {
            HeaderTimeout = headerTimeout ?? TimeSpan.FromSeconds(30),
            InactivityTimeout = inactivityTimeout ?? TimeSpan.FromSeconds(30),
            FreeSpaceBytes = freeSpace ?? (() => null),
        };

    private IReadOnlyList<string> upcoming() => library!.GetVideoPrefetchSnapshot(options.VideoPrefetchAhead).UpcomingVideos;

    [Fact]
    public async Task RunOnce_DownloadsEachUpcomingVideo_AndNothingBeyondTheLookAhead()
    {
        await seedAsync(videos: 10);
        var expected = upcoming();

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, immich.OpenedVideoIds);
        Assert.Equal(expected.Order(), store.ListCachedVideoIds().Order());
    }

    [Fact]
    public async Task RunOnce_IgnoresPhotosAndAlreadyCachedVideos()
    {
        await seedAsync(videos: 3, images: 3);
        options.VideoPrefetchAhead = 6;
        var videos = upcoming();
        Assert.Equal(3, videos.Count);
        await store.SaveVideoAsync(videos[1], ".mp4", s => s.WriteAsync(new byte[] { 7 }).AsTask());

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { videos[0], videos[2] }, immich.OpenedVideoIds);
        Assert.Equal(videos.Order(), store.ListCachedVideoIds().Order());
    }

    [Fact]
    public async Task RunOnce_DownloadsOneVideoAtATime()
    {
        await seedAsync(videos: 10);
        var inFlight = 0;
        var maxInFlight = 0;
        immich.OpenVideo = async (_, _) =>
        {
            maxInFlight = Math.Max(maxInFlight, Interlocked.Increment(ref inFlight));
            await Task.Delay(20);
            Interlocked.Decrement(ref inFlight);
            return mp4([1]);
        };

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, immich.OpenedVideoIds.Count);
        Assert.Equal(1, maxInFlight);
    }

    [Fact]
    public async Task RunOnce_SecondPassDownloadsNothingMore()
    {
        await seedAsync(videos: 10);
        var prefetcher = createPrefetcher();

        await prefetcher.RunOnceAsync(CancellationToken.None);
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, immich.OpenedVideoIds.Count);
    }

    [Fact]
    public async Task RunOnce_AfterTheLookAheadAdvances_FetchesTheNewlyUpcomingVideo()
    {
        await seedAsync(videos: 10);
        var prefetcher = createPrefetcher();
        await prefetcher.RunOnceAsync(CancellationToken.None);

        library!.Next();
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(4, immich.OpenedVideoIds.Count);
        Assert.Contains(upcoming()[^1], immich.OpenedVideoIds);
    }

    [Fact]
    public async Task RunOnce_WhenContentLengthIsOverTheCap_SkipsWithoutWritingAnything()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4(new byte[(2 * MB)]));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(store.ListCachedVideoIds());
        Assert.False(Directory.Exists(store.VideosPath) && Directory.GetFiles(store.VideosPath).Length > 0);
        Assert.Equal(3, immich.OpenedVideoIds.Count);
    }

    [Fact]
    public async Task RunOnce_WhenAStreamWithNoContentLengthExceedsTheCap_AbortsAndDeletesThePartialFile()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(2 * MB) };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
            return Task.FromResult(response);
        };

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(store.ListCachedVideoIds());
        Assert.Empty(Directory.GetFiles(store.VideosPath));
    }

    [Fact]
    public async Task RunOnce_AVideoExactlyAtTheCap_IsKept()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4(new byte[MB]));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, store.ListCachedVideoIds().Count);
    }

    [Fact]
    public async Task RunOnce_WhenADownloadFails_LogsAWarningMovesOnAndDoesNotRetryImmediately()
    {
        await seedAsync(videos: 10);
        var failing = upcoming()[0];
        immich.OpenVideo = (_, _) => immich.OpenedVideoIds[^1] == failing
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("boom"))
            : Task.FromResult(mp4([1]));
        var prefetcher = createPrefetcher();

        await prefetcher.RunOnceAsync(CancellationToken.None);
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, immich.OpenedVideoIds.Count(id => string.Equals(id, failing, StringComparison.Ordinal)));
        Assert.Equal(3, immich.OpenedVideoIds.Count);
        Assert.False(store.HasVideo(failing));
        Assert.Equal(2, store.ListCachedVideoIds().Count);
        Assert.Single(log.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(failing, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunOnce_WhenImmichAnswersNotFound_SkipsTheVideoWithAWarning()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(store.ListCachedVideoIds());
        Assert.Equal(3, log.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task RunOnce_WhenTheContentTypeIsNotPlayable_DoesNotCacheIt()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4([1], "application/octet-stream"));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(store.ListCachedVideoIds());
    }

    [Fact]
    public async Task RunOnce_KeepsTheUpstreamContainerType()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4([1], "video/quicktime"));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.True(store.TryGetVideo(upcoming()[0], out var path, out var contentType));
        Assert.EndsWith(".mov", path, StringComparison.Ordinal);
        Assert.Equal("video/quicktime", contentType);
    }

    [Fact]
    public async Task RunOnce_LogsIdSizeAndDurationAtInformationForEachDownload()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4(new byte[MB / 2]));

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        var line = Assert.Single(log.Entries, e => e.Level == LogLevel.Information && e.Message.Contains(upcoming()[0], StringComparison.Ordinal) && e.Message.StartsWith("Prefetched", StringComparison.Ordinal));
        Assert.Contains("0.50 MB", line.Message, StringComparison.Ordinal);
        Assert.Contains(" ms", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunOnce_WhenCancelledMidDownload_StopsAndLeavesNoPartialFile()
    {
        await seedAsync(videos: 10);
        using var cts = new CancellationTokenSource();
        immich.OpenVideo = (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(MB / 2, onFirstChunk: cts.Cancel) };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
            return Task.FromResult(response);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => createPrefetcher().RunOnceAsync(cts.Token));

        Assert.Empty(store.ListCachedVideoIds());
        Assert.Empty(Directory.GetFiles(store.VideosPath));
        Assert.Single(immich.OpenedVideoIds);
    }

    private static HttpResponseMessage stalledBody(Action? onFirstChunk = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(MB / 2, onFirstChunk, stallAfterFirstChunk: true) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        return response;
    }

    [Fact]
    public async Task RunOnce_WhenTheBodyStopsArriving_TimesOutDiscardsTheFileAndKeepsGoing()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(stalledBody());

        await createPrefetcher(inactivityTimeout: TimeSpan.FromMilliseconds(150)).RunOnceAsync(CancellationToken.None);

        // All three window videos stalled and timed out; the pass finished instead of hanging.
        Assert.Equal(3, immich.OpenedVideoIds.Count);
        Assert.Empty(store.ListCachedVideoIds());
        Assert.Empty(Directory.GetFiles(store.VideosPath));
        Assert.Equal(3, log.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("timed out", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RunOnce_WhenImmichNeverSendsHeaders_TimesOutAndMovesOn()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return mp4([1]);
        };

        await createPrefetcher(headerTimeout: TimeSpan.FromMilliseconds(100)).RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, immich.OpenedVideoIds.Count);
        Assert.Equal(3, log.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task RunOnce_WhenATimedOutVideoLeavesAndReentersTheLookAhead_ItIsTriedAgain()
    {
        await seedAsync(videos: 20);
        options.VideoPrefetchAhead = 1;
        var prefetcher = createPrefetcher(headerTimeout: TimeSpan.FromMilliseconds(50));
        var failing = true;
        immich.OpenVideo = async (_, ct) =>
        {
            if (failing)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }

            return mp4([1]);
        };
        var first = upcoming()[0];
        await prefetcher.RunOnceAsync(CancellationToken.None);
        await prefetcher.RunOnceAsync(CancellationToken.None);
        Assert.Single(immich.OpenedVideoIds);

        // Serving the failed video takes it out of the look-ahead; when the shuffle brings it back it is tried again.
        failing = false;
        for (var slide = 0; slide < 100 && !store.HasVideo(first); slide++)
        {
            library!.Next();
            await prefetcher.RunOnceAsync(CancellationToken.None);
        }

        Assert.True(store.HasVideo(first));
    }

    [Fact]
    public async Task RunOnce_WhenTheVideoIsRequestedLiveMidDownload_CancelsThePrefetchAndDeletesTheTempFile()
    {
        await seedAsync(videos: 10);
        options.VideoPrefetchAhead = 1;
        var target = upcoming()[0];
        IDisposable? live = null;
        immich.OpenVideo = (_, _) => Task.FromResult(stalledBody(onFirstChunk: () => live = coordinator.BeginLiveStream(target)));
        var prefetcher = createPrefetcher(inactivityTimeout: TimeSpan.FromSeconds(30));

        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(live);
        Assert.False(store.HasVideo(target));
        Assert.Empty(Directory.GetFiles(store.VideosPath));
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("streamed live", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Warning);
        live!.Dispose();
    }

    [Fact]
    public async Task RunOnce_DoesNotStartPrefetchingAVideoThatIsBeingStreamedLive()
    {
        await seedAsync(videos: 10);
        options.VideoPrefetchAhead = 1;
        var target = upcoming()[0];
        using var live = coordinator.BeginLiveStream(target);

        await createPrefetcher().RunOnceAsync(CancellationToken.None);

        Assert.Empty(immich.OpenedVideoIds);
        Assert.False(store.HasVideo(target));
    }

    [Fact]
    public async Task RunOnce_WhenServedWhileDownloading_TheLiveRequestCancelsTheDownloadThroughTheCoordinator()
    {
        await seedAsync(videos: 10);
        options.VideoPrefetchAhead = 1;
        var target = upcoming()[0];
        var started = new TaskCompletionSource();
        immich.OpenVideo = (_, ct) =>
        {
            started.SetResult();
            return Task.FromResult(stalledBody());
        };
        var pass = createPrefetcher().RunOnceAsync(CancellationToken.None);
        await started.Task;

        // What the endpoint does when /video arrives for a video that is still downloading.
        using (coordinator.BeginLiveStream(target))
        {
            await pass.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.Empty(Directory.GetFiles(store.VideosPath));
        Assert.False(coordinator.IsLive(target));
    }

    [Fact]
    public async Task RunOnce_WhenTheCacheDriveIsNearlyFull_SkipsThePrefetch()
    {
        await seedAsync(videos: 10);

        await createPrefetcher(freeSpace: () => (1024 * MB) - 1).RunOnceAsync(CancellationToken.None);
        Assert.Empty(immich.OpenedVideoIds);
        Assert.Equal(3, log.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("free", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RunOnce_WhenTheDownloadItselfWouldLeaveTooLittleSpace_SkipsAfterTheHeaders()
    {
        await seedAsync(videos: 10);
        immich.OpenVideo = (_, _) => Task.FromResult(mp4(new byte[MB / 2]));

        await createPrefetcher(freeSpace: () => (1024 * MB) + (MB / 4)).RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, immich.OpenedVideoIds.Count);
        Assert.Empty(store.ListCachedVideoIds());
    }

    [Fact]
    public async Task RunOnce_WithPlentyOfFreeSpace_Downloads()
    {
        await seedAsync(videos: 10);

        await createPrefetcher(freeSpace: () => 5L * 1024 * MB).RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, store.ListCachedVideoIds().Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ExecuteAsync_WhenTheMaxSizeIsZeroOrLess_PrefetchIsDisabled(int maxMb)
    {
        await seedAsync(videos: 10);
        options.VideoPrefetchMaxMB = maxMb;
        using var prefetcher = createPrefetcher();

        await prefetcher.StartAsync(CancellationToken.None);
        await prefetcher.StopAsync(CancellationToken.None);

        Assert.Empty(immich.OpenedVideoIds);
    }

    [Fact]
    public async Task ExecuteAsync_RemovesStrayFilesAtStartupWithoutBlockingHostStart()
    {
        await seedAsync(videos: 10);
        Directory.CreateDirectory(store.VideosPath);
        await File.WriteAllBytesAsync(Path.Combine(store.VideosPath, "stray.tmp"), [1]);
        using var prefetcher = createPrefetcher();

        // Cleanup runs after the first yield, so StartAsync itself never depends on it.
        await prefetcher.StartAsync(CancellationToken.None);
        await waitForAsync(() => store.ListCachedVideoIds().Count == 3);
        await prefetcher.StopAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(store.VideosPath, "stray.tmp")));
    }

    [Fact]
    public async Task Next_AfterThePrefetcherIsDisposed_StillWorks()
    {
        await seedAsync(videos: 5);
        var prefetcher = createPrefetcher();
        await prefetcher.StartAsync(CancellationToken.None);

        // A /next that races shutdown invokes the handler on a disposed semaphore; it must not surface as a 500.
        prefetcher.Dispose();

        for (var i = 0; i < 3; i++)
        {
            Assert.NotNull(library!.Next());
        }
    }

    [Fact]
    public void SelectDownloads_KeepsPlayOrderAndSkipsCachedAndBackedOffIds()
    {
        var chosen = VideoPrefetcher.SelectDownloads(["a", "b", "c", "d"], id => string.Equals(id, "b", StringComparison.Ordinal), new HashSet<string> { "c" });

        Assert.Equal(["a", "d"], chosen);
    }

    [Fact]
    public async Task RunOnce_EvictsAServedVideoAfterTenMoreSlidesAndKeepsItUntilThen()
    {
        await seedAsync(videos: 20);
        options.VideoPrefetchAhead = 2;
        var prefetcher = createPrefetcher();
        await prefetcher.RunOnceAsync(CancellationToken.None);
        var first = library!.Next()!.Id;
        Assert.True(store.HasVideo(first));

        for (var i = 1; i < VideoEvictionPolicy.SlidesBeforeEviction; i++)
        {
            library.Next();
            await prefetcher.RunOnceAsync(CancellationToken.None);
            Assert.True(store.HasVideo(first), $"still cached after {i} more slides");
        }

        library.Next();
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.False(store.HasVideo(first));
    }

    [Fact]
    public async Task RunOnce_KeepsOnlyAWindowOfVideosOnDiskWhileSlidesAdvance()
    {
        await seedAsync(videos: 30);
        var prefetcher = createPrefetcher();

        for (var i = 0; i < 25; i++)
        {
            await prefetcher.RunOnceAsync(CancellationToken.None);
            library!.Next();
        }

        // Up to 10 recently served videos plus the 3 upcoming ones.
        Assert.InRange(store.ListCachedVideoIds().Count, 3, 13);
    }

    [Fact]
    public async Task RunOnce_RemovesCachedVideosThatAreNoLongerVisible()
    {
        await seedAsync(videos: 10);
        var prefetcher = createPrefetcher();
        await prefetcher.RunOnceAsync(CancellationToken.None);
        var hidden = upcoming()[0];

        library!.Hide(hidden);
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.False(store.HasVideo(hidden));
    }

    [Fact]
    public async Task RunOnce_RemovesCachedVideosThatLeftTheAlbum_AfterAReload()
    {
        await seedAsync(videos: 10);
        var prefetcher = createPrefetcher();
        await prefetcher.RunOnceAsync(CancellationToken.None);
        var cached = store.ListCachedVideoIds();
        store.SaveManifest(store.LoadManifest().Where(p => !cached.Contains(p.AssetId, StringComparer.Ordinal)).ToList());

        library!.Reload();
        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.DoesNotContain(cached[0], store.ListCachedVideoIds());
    }

    [Fact]
    public async Task RunOnce_WhenAVideoIsBeingServedAndCannotBeDeleted_RetriesOnALaterPass()
    {
        await seedAsync(videos: 10);
        var prefetcher = createPrefetcher();
        await prefetcher.RunOnceAsync(CancellationToken.None);
        var hidden = upcoming()[0];
        var path = Path.Combine(store.VideosPath, hidden + ".mp4");

        // Windows refuses to delete a file that is open; Linux unlinks it and the open handle keeps working.
        await using (var inUse = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            library!.Hide(hidden);
            await prefetcher.RunOnceAsync(CancellationToken.None);
            if (OperatingSystem.IsWindows())
            {
                Assert.True(File.Exists(path));
            }
        }

        await prefetcher.RunOnceAsync(CancellationToken.None);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Library_SignalsUpcomingChangedOnNextAndReload()
    {
        await seedAsync(videos: 5);
        var signals = 0;
        library!.UpcomingChanged += () => signals++;

        library.Next();
        library.Reload();

        Assert.Equal(2, signals);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAPassWhenTheSlideshowAdvancesAndStopsOnShutdown()
    {
        await seedAsync(videos: 10);
        using var prefetcher = createPrefetcher();

        await prefetcher.StartAsync(CancellationToken.None);
        await waitForAsync(() => store.ListCachedVideoIds().Count == 3);
        library!.Next();
        await waitForAsync(() => store.ListCachedVideoIds().Count == 4);
        await prefetcher.StopAsync(CancellationToken.None);

        Assert.Equal(4, store.ListCachedVideoIds().Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrefetchAheadIsZero_DoesNothing()
    {
        await seedAsync(videos: 10);
        options.VideoPrefetchAhead = 0;
        using var prefetcher = createPrefetcher();

        await prefetcher.StartAsync(CancellationToken.None);
        await prefetcher.StopAsync(CancellationToken.None);

        Assert.Empty(immich.OpenedVideoIds);
    }

    private static async Task waitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting for the prefetcher");
            await Task.Delay(20);
        }
    }

    /// <summary>A body that reports no Content-Length, streamed in chunks.</summary>
    private sealed class UnknownLengthContent(long totalBytes, Action? onFirstChunk = null, bool stallAfterFirstChunk = false) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var chunk = new byte[64 * 1024];
            for (long sent = 0; sent < totalBytes; sent += chunk.Length)
            {
                await stream.WriteAsync(chunk);
            }
        }

        protected override async Task<Stream> CreateContentReadStreamAsync()
        {
            await Task.CompletedTask;
            return new ChunkStream(totalBytes, onFirstChunk, stallAfterFirstChunk);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ChunkStream(long totalBytes, Action? onFirstChunk, bool stallAfterFirstChunk = false) : Stream
    {
        private long sent;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sent > 0 && stallAfterFirstChunk)
            {
                // A peer that went silent: the read only ends when the caller's token fires.
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (sent == 0)
            {
                onFirstChunk?.Invoke();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(Math.Min(buffer.Length, 64 * 1024), totalBytes - sent);
            sent += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
