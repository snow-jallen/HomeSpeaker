using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.YouTube;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.YouTube;

public sealed class YouTubeDownloadQueueTests : IAsyncLifetime
{
    private readonly FakeDownloader downloader = new();
    private readonly YouTubeDownloadQueue queue;

    public YouTubeDownloadQueueTests()
    {
        queue = new YouTubeDownloadQueue(downloader, NullLogger<YouTubeDownloadQueue>.Instance);
    }

    public Task InitializeAsync() => queue.StartAsync(CancellationToken.None);

    public async Task DisposeAsync()
    {
        downloader.ReleaseAll();
        await queue.StopAsync(CancellationToken.None);
        queue.Dispose();
    }

    [Fact]
    public async Task DownloadsEveryVideoInOrderOneAtATime()
    {
        queue.Enqueue([request("a"), request("b"), request("c")]);

        await downloader.Started("a");
        Assert.Equal([YouTubeDownloadStatus.Downloading, YouTubeDownloadStatus.Queued, YouTubeDownloadStatus.Queued], statuses());

        downloader.Finish("a");
        await downloader.Started("b");
        downloader.Finish("b", alreadyThere: true);
        await downloader.Started("c");
        downloader.Finish("c");
        await waitUntil(() => queue.Snapshot().All(d => d.IsFinished));

        Assert.Equal([YouTubeDownloadStatus.Done, YouTubeDownloadStatus.AlreadyHad, YouTubeDownloadStatus.Done], statuses());
        Assert.Equal(["a", "b", "c"], downloader.Order);
    }

    [Fact]
    public async Task AVideoAlreadyWaitingIsNotQueuedTwice()
    {
        Assert.Equal(2, queue.Enqueue([request("a"), request("b")]));
        Assert.Equal(1, queue.Enqueue([request("b"), request("c")]));

        Assert.Equal(["a", "b", "c"], queue.Snapshot().Select(d => d.VideoId));
        await downloader.Started("a");
    }

    [Fact]
    public async Task ASavedVideoIsNotQueuedAgain()
    {
        queue.Enqueue([request("a")]);
        await downloader.Started("a");
        downloader.Finish("a");
        await waitUntil(() => queue.Find("a")!.IsFinished);

        Assert.Equal(1, queue.Enqueue([request("a"), request("b")]));
        Assert.Equal(["a", "b"], queue.Snapshot().Select(d => d.VideoId));
    }

    [Fact]
    public async Task AFailedVideoCanBeQueuedAgain()
    {
        queue.Enqueue([request("a")]);
        await downloader.Started("a");
        downloader.Fail("a", "network down");
        await waitUntil(() => queue.Find("a")!.IsFinished);

        var failed = queue.Find("a")!;
        Assert.Equal(YouTubeDownloadStatus.Failed, failed.Status);
        Assert.Equal("network down", failed.Error);

        Assert.Equal(1, queue.Enqueue([request("a")]));
        Assert.NotSame(failed, queue.Find("a"));
        Assert.Equal(2, queue.Snapshot().Count(d => d.VideoId == "a"));
    }

    [Fact]
    public async Task OneFailureDoesNotStopTheRest()
    {
        queue.Enqueue([request("a"), request("b")]);
        await downloader.Started("a");
        downloader.Fail("a", "private video");
        await downloader.Started("b");
        downloader.Finish("b");
        await waitUntil(() => queue.Snapshot().All(d => d.IsFinished));

        Assert.Equal([YouTubeDownloadStatus.Failed, YouTubeDownloadStatus.Done], statuses());
    }

    [Fact]
    public async Task CancelAllStopsTheCurrentDownloadAndSkipsTheRest()
    {
        queue.Enqueue([request("a"), request("b"), request("c")]);
        await downloader.Started("a");

        queue.CancelAll();
        await waitUntil(() => queue.Snapshot().All(d => d.IsFinished));

        Assert.Equal([YouTubeDownloadStatus.Cancelled, YouTubeDownloadStatus.Cancelled, YouTubeDownloadStatus.Cancelled], statuses());
        Assert.Equal(["a"], downloader.Order);
    }

    [Fact]
    public async Task ClearFinishedKeepsWhatIsStillGoing()
    {
        queue.Enqueue([request("a"), request("b")]);
        await downloader.Started("a");
        downloader.Finish("a");
        await downloader.Started("b");

        queue.ClearFinished();

        Assert.Equal(["b"], queue.Snapshot().Select(d => d.VideoId));
    }

    [Fact]
    public async Task ProgressIsReportedOnTheItem()
    {
        queue.Enqueue([request("a")]);
        await downloader.Started("a");

        downloader.Report("a", 0.42);

        Assert.Equal(0.42, queue.Find("a")!.Progress, 3);
    }

    [Fact]
    public void PlaylistRequestsShareTheTitleAndAreNumbered()
    {
        var contents = new YouTubeLinkContents("Road Trip", [video("a"), video("b")]);

        var requests = YouTubeDownloads.RequestsFor(contents).ToList();

        Assert.Equal([new YouTubeDownloadRequest("a", "Song a", "Channel", "Road Trip", 1), new YouTubeDownloadRequest("b", "Song b", "Channel", "Road Trip", 2)], requests);
    }

    [Fact]
    public void ASingleVideoRequestHasNoCollection()
    {
        var requests = YouTubeDownloads.RequestsFor(new YouTubeLinkContents(null, [video("a")])).ToList();

        Assert.Equal([new YouTubeDownloadRequest("a", "Song a", "Channel")], requests);
    }

    private static YouTubeDownloadRequest request(string id) => new(id, "Song " + id, "Channel");

    private static VideoDto video(string id) => new("Song " + id, id, "https://youtu.be/" + id, null, "Channel", null);

    private List<YouTubeDownloadStatus> statuses() => queue.Snapshot().Select(d => d.Status).ToList();

    private static async Task waitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting");
            await Task.Delay(10);
        }
    }

    /// <summary>Each download waits until the test finishes, fails or cancels it.</summary>
    private sealed class FakeDownloader : IYouTubeDownloader
    {
        private readonly Dictionary<string, TaskCompletionSource<bool>> results = [];
        private readonly Dictionary<string, TaskCompletionSource> started = [];
        private readonly Dictionary<string, IProgress<double>> progress = [];
        private readonly Lock gate = new();

        public List<string> Order { get; } = [];

        public async Task<bool> DownloadAsync(YouTubeDownloadRequest request, IProgress<double> progress, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> result;
            lock (gate)
            {
                Order.Add(request.VideoId);
                result = resultFor(request.VideoId);
                this.progress[request.VideoId] = progress;
                startedFor(request.VideoId).TrySetResult();
            }

            using var registration = cancellationToken.Register(() => result.TrySetCanceled(cancellationToken));
            return await result.Task;
        }

        public Task Started(string id)
        {
            lock (gate)
            {
                return startedFor(id).Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        public void Finish(string id, bool alreadyThere = false)
        {
            lock (gate)
            {
                resultFor(id).TrySetResult(!alreadyThere);
            }
        }

        public void Fail(string id, string message)
        {
            lock (gate)
            {
                resultFor(id).TrySetException(new InvalidOperationException(message));
            }
        }

        public void Report(string id, double value)
        {
            lock (gate)
            {
                progress[id].Report(value);
            }
        }

        public void ReleaseAll()
        {
            lock (gate)
            {
                foreach (var r in results.Values)
                {
                    r.TrySetResult(true);
                }
            }
        }

        private TaskCompletionSource<bool> resultFor(string id) =>
            results.TryGetValue(id, out var r) ? r : results[id] = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource startedFor(string id) =>
            started.TryGetValue(id, out var s) ? s : started[id] = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
