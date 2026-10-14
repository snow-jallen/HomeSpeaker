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
    private static readonly TimeSpan fallbackPoll = TimeSpan.FromSeconds(60);

    private readonly PhotoLibrary library;
    private readonly PhotoCacheStore store;
    private readonly IImmichClient immich;
    private readonly IOptions<ImmichOptions> options;
    private readonly TimeProvider timeProvider;
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
        ILogger<VideoPrefetcher> logger)
    {
        this.library = library;
        this.store = store;
        this.immich = immich;
        this.options = options;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured || settings.VideoPrefetchAhead <= 0)
        {
            logger.LogInformation("Video prefetch is disabled (Immich not configured or Immich:VideoPrefetchAhead is 0)");
            return;
        }

        store.DeleteStrayVideoFiles();
        library.UpcomingChanged += requestPass;
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
            library.UpcomingChanged -= requestPass;
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

    private void requestPass()
    {
        try
        {
            wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A pass is already requested.
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

    private async Task downloadAsync(string assetId, CancellationToken cancellationToken)
    {
        var maxBytes = Math.Max(1, options.Value.VideoPrefetchMaxMB) * BytesPerMB;
        var started = timeProvider.GetTimestamp();
        try
        {
            using var response = await immich.OpenVideoAsync(assetId, null, cancellationToken);
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

            long written = 0;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await store.SaveVideoAsync(assetId, extension, async destination =>
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maxBytes)
                    {
                        throw new VideoOverCapException();
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            });

            logger.LogInformation(
                "Prefetched video {AssetId}: {SizeMB} MB in {ElapsedMs} ms",
                assetId,
                (written / (double)BytesPerMB).ToString("0.00", CultureInfo.InvariantCulture),
                (long)timeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (VideoOverCapException)
        {
            logger.LogInformation("Video prefetch: {AssetId} grew past the {MaxMB} MB cap; discarded, it will stream live", assetId, options.Value.VideoPrefetchMaxMB);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Video prefetch failed for {AssetId}; it will stream live", assetId);
        }
    }

    private sealed class VideoOverCapException : Exception;
}
