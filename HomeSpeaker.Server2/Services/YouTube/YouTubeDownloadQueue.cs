using System.Threading.Channels;

namespace HomeSpeaker.Server2.Services.YouTube;

/// <summary>
/// Downloads YouTube videos one at a time on the server, so a long playlist keeps going after the
/// page that started it is closed. Pages read <see cref="Snapshot"/> and re-render on <see cref="Changed"/>.
/// </summary>
public sealed class YouTubeDownloadQueue : BackgroundService
{
    private const int FinishedToKeep = 200;

    private readonly Channel<YouTubeDownload> pending = Channel.CreateUnbounded<YouTubeDownload>(new UnboundedChannelOptions { SingleReader = true });
    private readonly List<YouTubeDownload> items = [];
    private readonly Lock gate = new();
    private readonly IYouTubeDownloader downloader;
    private readonly ILogger<YouTubeDownloadQueue> logger;
#pragma warning disable CA2213 // Not owned here: ExecuteAsync's loop creates and disposes each one; this only points at it to cancel.
    private CancellationTokenSource? current;
#pragma warning restore CA2213

    public YouTubeDownloadQueue(IYouTubeDownloader downloader, ILogger<YouTubeDownloadQueue> logger)
    {
        this.downloader = downloader;
        this.logger = logger;
    }

    /// <summary>Raised from background threads whenever an item is added or changes status or progress.</summary>
    public event Action? Changed;

    public IReadOnlyList<YouTubeDownload> Snapshot()
    {
        lock (gate)
        {
            return [.. items];
        }
    }

    /// <summary>The most recent entry for a video, if it has been queued.</summary>
    public YouTubeDownload? Find(string videoId)
    {
        lock (gate)
        {
            return items.LastOrDefault(i => i.VideoId == videoId);
        }
    }

    /// <summary>
    /// Queues the requests, skipping any video already waiting, downloading or saved (pasting a playlist
    /// again only retries the ones that failed or were cancelled). Returns how many were added.
    /// </summary>
    public int Enqueue(IEnumerable<YouTubeDownloadRequest> requests)
    {
        var added = 0;
        lock (gate)
        {
            foreach (var request in requests)
            {
                if (items.Any(i => i.VideoId == request.VideoId && i.Status is not (YouTubeDownloadStatus.Failed or YouTubeDownloadStatus.Cancelled)))
                {
                    continue;
                }

                var item = new YouTubeDownload(request);
                items.Add(item);
                pending.Writer.TryWrite(item);
                added++;
            }
        }

        if (added > 0)
        {
            Changed?.Invoke();
        }

        return added;
    }

    /// <summary>Stops the download in progress and drops everything still waiting.</summary>
    public void CancelAll()
    {
        lock (gate)
        {
            foreach (var item in items.Where(i => i.Status == YouTubeDownloadStatus.Queued))
            {
                item.Status = YouTubeDownloadStatus.Cancelled;
            }

            current?.Cancel();
        }

        Changed?.Invoke();
    }

    public void ClearFinished()
    {
        lock (gate)
        {
            items.RemoveAll(i => i.IsFinished);
        }

        Changed?.Invoke();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in pending.Reader.ReadAllAsync(stoppingToken))
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            lock (gate)
            {
                if (item.Status != YouTubeDownloadStatus.Queued)
                {
                    continue;
                }

                item.Status = YouTubeDownloadStatus.Downloading;
                current = cancel;
            }

            Changed?.Invoke();
            await runAsync(item, cancel.Token, stoppingToken);

            lock (gate)
            {
                current = null;
                trimFinished();
            }

            Changed?.Invoke();
        }
    }

    private async Task runAsync(YouTubeDownload item, CancellationToken cancellationToken, CancellationToken stoppingToken)
    {
        try
        {
            var downloaded = await downloader.DownloadAsync(item.Request, new ItemProgress(item, () => Changed?.Invoke()), cancellationToken);
            item.Progress = 1;
            item.Status = downloaded ? YouTubeDownloadStatus.Done : YouTubeDownloadStatus.AlreadyHad;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            item.Status = YouTubeDownloadStatus.Cancelled;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "YouTube download failed for {Title} ({VideoId})", item.Title, item.VideoId);
            item.Error = ex.Message;
            item.Status = YouTubeDownloadStatus.Failed;
        }
    }

    private void trimFinished()
    {
        var excess = items.Count(i => i.IsFinished) - FinishedToKeep;
        for (var i = 0; i < items.Count && excess > 0; i++)
        {
            if (items[i].IsFinished)
            {
                items.RemoveAt(i--);
                excess--;
            }
        }
    }

    /// <summary>Writes progress straight onto the item (no sync context hop), notifying at most once per percent.</summary>
    private sealed class ItemProgress(YouTubeDownload item, Action notify) : IProgress<double>
    {
        public void Report(double value)
        {
            var previous = item.Progress;
            item.Progress = Math.Clamp(value, 0, 1);
            if (Math.Floor(item.Progress * 100) != Math.Floor(previous * 100))
            {
                notify();
            }
        }
    }
}
