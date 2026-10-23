using System.Globalization;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Keeps the next few slideshow videos downloaded to the Pi so playback does not stall on a live relay over the VPN.
/// Wakes when the slide order changes (PhotoLibrary.UpcomingChanged) with a slow poll as a fallback, evicts cached
/// videos that are no longer needed, then downloads the missing upcoming ones one at a time.
/// </summary>
public sealed class VideoPrefetcher : BackgroundService
{
    private const long BytesPerMB = 1024 * 1024;
    private const long MinFreeBytes = 1024 * BytesPerMB;
    private static readonly TimeSpan fallbackPoll = TimeSpan.FromSeconds(60);

    private readonly PhotoLibrary library;
    private readonly PhotoCacheStore store;
    private readonly IImmichClient immich;
    private readonly IOptions<ImmichOptions> options;
    private readonly TimeProvider timeProvider;
    private readonly VideoStreamCoordinator coordinator;
    private readonly ILogger<VideoPrefetcher> logger;
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly VideoEvictionPolicy eviction = new();
    private readonly HashSet<string> backedOff = new(StringComparer.Ordinal);

    public VideoPrefetcher(
        PhotoLibrary library,
        PhotoCacheStore store,
        IImmichClient immich,
        IOptions<ImmichOptions> options,
        TimeProvider timeProvider,
        VideoStreamCoordinator coordinator,
        ILogger<VideoPrefetcher> logger)
    {
        this.library = library;
        this.store = store;
        this.immich = immich;
        this.options = options;
        this.timeProvider = timeProvider;
        this.coordinator = coordinator;
        this.logger = logger;
    }

    /// <summary>How long Immich may take to send response headers before a prefetch is abandoned.</summary>
    public TimeSpan HeaderTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a prefetch may go without receiving a byte before it is abandoned.</summary>
    public TimeSpan InactivityTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Free bytes on the cache drive; null means unknown (no guard). A prefetch must leave 1 GB free.</summary>
    public Func<long?> FreeSpaceBytes { get; init; } = () => null;

    /// <summary>False when the active photo source is not Immich (the folder source has no videos).</summary>
    public bool SourceIsImmich { get; init; } = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!SourceIsImmich || !settings.IsConfigured || settings.VideoPrefetchAhead <= 0 || settings.VideoPrefetchMaxMB <= 0)
        {
            logger.LogInformation("Video prefetch is disabled (Immich not configured or not the photo source, or Immich:VideoPrefetchAhead or Immich:VideoPrefetchMaxMB is 0 or less)");
            return;
        }

        // Keep the cleanup off the host startup path: a failure there must never stop the app from starting.
        await Task.Yield();
        store.DeleteStrayVideoFiles();
        library.UpcomingChanged += RequestPass;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Video prefetch pass failed unexpectedly");
                }

                try
                {
                    await wake.WaitAsync(fallbackPoll, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            library.UpcomingChanged -= RequestPass;
        }
    }

    /// <summary>One pass: evict what is no longer needed, then download the upcoming videos that are not cached yet.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var ahead = options.Value.VideoPrefetchAhead;
        evict(library.GetVideoPrefetchSnapshot(ahead));

        // Each download can take minutes, so look at the slide order again after every one.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = library.GetVideoPrefetchSnapshot(ahead);

            // A failed or skipped video is retried only after it has left the look-ahead and come back.
            backedOff.IntersectWith(snapshot.UpcomingVideos);
            var next = SelectDownloads(snapshot.UpcomingVideos, store.HasVideo, backedOff).FirstOrDefault();
            if (next is null)
            {
                return;
            }

            backedOff.Add(next);
            await downloadAsync(next, cancellationToken);
        }
    }

    /// <summary>The upcoming videos that still need downloading, in play order: not already cached, not backed off.</summary>
    public static IEnumerable<string> SelectDownloads(IEnumerable<string> upcomingVideos, Func<string, bool> isCached, ISet<string> backedOff) =>
        upcomingVideos.Where(id => !backedOff.Contains(id) && !isCached(id));

    public override void Dispose()
    {
        wake.Dispose();
        base.Dispose();
    }

    /// <summary>Wakes the loop for a pass. Safe to call at any time, including while shutting down.</summary>
    public void RequestPass()
    {
        try
        {
            wake.Release();
        }
        catch (Exception ex) when (ex is SemaphoreFullException or ObjectDisposedException)
        {
            // A pass is already requested, or the prefetcher is shutting down.
        }
    }

    private void evict(VideoPrefetchSnapshot snapshot)
    {
        foreach (var id in eviction.SelectEvictions(store.ListCachedVideoIds(), snapshot))
        {
            if (store.TryDeleteVideo(id))
            {
                logger.LogInformation("Evicted cached video {AssetId}", id);
            }
        }
    }

    private async Task downloadAsync(string assetId, CancellationToken stoppingToken)
    {
        using var lease = coordinator.TryBeginPrefetch(assetId, stoppingToken);
        if (lease is null)
        {
            logger.LogDebug("Video prefetch: {AssetId} is being streamed live; not prefetching it", assetId);
            return;
        }

        var maxBytes = options.Value.VideoPrefetchMaxMB * BytesPerMB;
        var started = timeProvider.GetTimestamp();
        if (freeSpaceTooLow(0, assetId))
        {
            return;
        }

        // One token covers the header wait and every body read: armed with the header timeout, then re-armed before each read.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lease.Token);
        try
        {
            timeout.CancelAfter(HeaderTimeout);
            using var response = await immich.OpenVideoAsync(assetId, null, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Video prefetch: Immich returned {StatusCode} for {AssetId}; it will stream live", (int)response.StatusCode, assetId);
                return;
            }

            var length = response.Content.Headers.ContentLength;
            if (length > maxBytes)
            {
                logger.LogInformation(
                    "Video prefetch: skipping {AssetId}, {SizeMB} MB is over the {MaxMB} MB cap; it will stream live",
                    assetId,
                    length.Value / BytesPerMB,
                    options.Value.VideoPrefetchMaxMB);
                return;
            }

            var contentType = response.Content.Headers.ContentType?.ToString();
            if (PhotoCacheStore.VideoExtensionFor(contentType) is not { } extension)
            {
                logger.LogInformation("Video prefetch: skipping {AssetId}, content type {ContentType} is not cacheable; it will stream live", assetId, contentType);
                return;
            }

            // Without a Content-Length, assume the worst case: the download runs up to the cap.
            if (freeSpaceTooLow(length ?? maxBytes, assetId))
            {
                return;
            }

            long written = 0;
            timeout.CancelAfter(InactivityTimeout);
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await store.SaveVideoAsync(assetId, extension, async destination =>
            {
                var buffer = new byte[81920];
                while (true)
                {
                    timeout.CancelAfter(InactivityTimeout);
                    var read = await source.ReadAsync(buffer, timeout.Token);
                    if (read == 0)
                    {
                        break;
                    }

                    written += read;
                    if (written > maxBytes)
                    {
                        throw new VideoOverCapException();
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                }
            });

            logger.LogInformation(
                "Prefetched video {AssetId}: {SizeMB} MB in {ElapsedMs} ms",
                assetId,
                (written / (double)BytesPerMB).ToString("0.00", CultureInfo.InvariantCulture),
                (long)timeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
            // A live stream of this video started; the temp file is already gone and the live stream gets the whole link.
            logger.LogInformation("Video prefetch: {AssetId} is now being streamed live; prefetch cancelled", assetId);
        }
        catch (VideoOverCapException)
        {
            logger.LogInformation("Video prefetch: {AssetId} grew past the {MaxMB} MB cap; discarded, it will stream live", assetId, options.Value.VideoPrefetchMaxMB);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Video prefetch timed out for {AssetId} (no response headers in {HeaderSeconds}s or no data for {InactivitySeconds}s); it will stream live", assetId, HeaderTimeout.TotalSeconds, InactivityTimeout.TotalSeconds);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Video prefetch failed for {AssetId}; it will stream live", assetId);
        }
    }

    /// <summary>True (after logging) when downloading <paramref name="sizeBytes"/> more would leave the cache drive under 1 GB free.</summary>
    private bool freeSpaceTooLow(long sizeBytes, string assetId)
    {
        if (FreeSpaceBytes() is not { } free || free - sizeBytes >= MinFreeBytes)
        {
            return false;
        }

        logger.LogWarning(
            "Video prefetch: skipping {AssetId}, only {FreeMB} MB free on the cache drive (keeping at least 1024 MB); it will stream live",
            assetId,
            free / BytesPerMB);
        return true;
    }

    private sealed class VideoOverCapException : Exception;
}
