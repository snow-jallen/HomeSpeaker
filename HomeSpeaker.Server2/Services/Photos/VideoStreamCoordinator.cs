namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Keeps the video prefetcher and the live proxy from pulling the same video over the same link at once. The proxy
/// registers each live stream (which cancels an in-flight prefetch of that video), and the prefetcher refuses to start
/// on a video that is being streamed live. Thread-safe; registered as a singleton.
/// </summary>
public sealed class VideoStreamCoordinator
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, int> liveStreams = new(StringComparer.Ordinal);
    private string? prefetchId;
    private CancellationTokenSource? prefetchCts;

    /// <summary>Registers a live proxy stream. Cancels an in-flight prefetch of the same video. Dispose when the stream ends.</summary>
    public IDisposable BeginLiveStream(string assetId)
    {
        lock (gate)
        {
            liveStreams[assetId] = liveStreams.GetValueOrDefault(assetId) + 1;
            if (string.Equals(prefetchId, assetId, StringComparison.Ordinal))
            {
                prefetchCts?.Cancel();
            }
        }

        return new LiveStreamLease(this, assetId);
    }

    public bool IsLive(string assetId)
    {
        lock (gate)
        {
            return liveStreams.ContainsKey(assetId);
        }
    }

    /// <summary>
    /// Starts a prefetch of <paramref name="assetId"/>, or returns null when that video is being streamed live.
    /// The lease's token is cancelled when a live stream for the video starts, or when <paramref name="stoppingToken"/> is.
    /// </summary>
    public PrefetchLease? TryBeginPrefetch(string assetId, CancellationToken stoppingToken)
    {
        lock (gate)
        {
            if (liveStreams.ContainsKey(assetId))
            {
                return null;
            }

            prefetchCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            prefetchId = assetId;
            return new PrefetchLease(this, prefetchCts);
        }
    }

    private void endLive(string assetId)
    {
        lock (gate)
        {
            if (liveStreams.TryGetValue(assetId, out var count) && count > 1)
            {
                liveStreams[assetId] = count - 1;
            }
            else
            {
                liveStreams.Remove(assetId);
            }
        }
    }

    private void endPrefetch(CancellationTokenSource cts)
    {
        lock (gate)
        {
            if (ReferenceEquals(prefetchCts, cts))
            {
                prefetchCts = null;
                prefetchId = null;
            }

            cts.Dispose();
        }
    }

    private sealed class LiveStreamLease(VideoStreamCoordinator owner, string assetId) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.endLive(assetId);
            }
        }
    }

    public sealed class PrefetchLease : IDisposable
    {
        private readonly VideoStreamCoordinator owner;
        private readonly CancellationTokenSource cts;

        internal PrefetchLease(VideoStreamCoordinator owner, CancellationTokenSource cts)
        {
            this.owner = owner;
            this.cts = cts;
            Token = cts.Token;
        }

        public CancellationToken Token { get; }

        public void Dispose() => owner.endPrefetch(cts);
    }
}
