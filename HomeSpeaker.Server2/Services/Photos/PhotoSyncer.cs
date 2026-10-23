using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>One pass of mirroring the configured photo source (Immich album or local folder) into the local photo cache.</summary>
public sealed class PhotoSyncer
{
    private readonly IPhotoSource source;
    private readonly PhotoCacheStore store;
    private readonly PhotoLibrary library;
    private readonly IOptions<ImmichOptions> options;
    private readonly ILogger<PhotoSyncer> logger;
    private readonly Random random;

    public PhotoSyncer(IPhotoSource source, PhotoCacheStore store, PhotoLibrary library, IOptions<ImmichOptions> options, ILogger<PhotoSyncer> logger)
        : this(source, store, library, options, logger, Random.Shared)
    {
    }

    public PhotoSyncer(IPhotoSource source, PhotoCacheStore store, PhotoLibrary library, IOptions<ImmichOptions> options, ILogger<PhotoSyncer> logger, Random random)
    {
        this.source = source;
        this.store = store;
        this.library = library;
        this.options = options;
        this.logger = logger;
        this.random = random;
    }

    private const int AssetTimeoutSeconds = 60;
    private static readonly TimeSpan staleTempAge = TimeSpan.FromHours(1);

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ImmichAsset> assets;
        try
        {
            assets = await source.ListAssetsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (ex is HttpRequestException { StatusCode: { } status })
            {
                logger.LogWarning(ex, "Photo source listing failed (HTTP {StatusCode}); keeping the existing photo cache", status);
            }
            else
            {
                logger.LogWarning(ex, "Photo source listing failed; keeping the existing photo cache");
            }

            return false;
        }

        var existing = store.LoadManifest()
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // The manifest is a full album index. Images that are not cached stay hidden until a rotation brings them in.
        var entries = new Dictionary<string, PhotoEntry>(assets.Count, StringComparer.Ordinal);
        var images = new List<ImmichAsset>();
        var videos = 0;
        foreach (var asset in assets)
        {
            if (entries.ContainsKey(asset.Id))
            {
                continue;
            }

            existing.TryGetValue(asset.Id, out var previous);
            if (asset.IsVideo)
            {
                // Videos are streamed from Immich on demand: no download, no face lookup, only a manifest entry.
                videos++;
                entries[asset.Id] = previous is null
                    ? new PhotoEntry
                    {
                        AssetId = asset.Id,
                        TakenAt = asset.TakenAt,
                        City = asset.City,
                        State = asset.State,
                        Country = asset.Country,
                        IsVideo = true,
                        DurationMs = asset.DurationMs,
                    }
                    : previous with { TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country, IsVideo = true, DurationMs = asset.DurationMs };
                continue;
            }

            images.Add(asset);
            entries[asset.Id] = previous is null
                ? new PhotoEntry { AssetId = asset.Id, TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country }
                : previous with { TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country };
        }

        // Reclaim disk first: files for assets that left the album, plus orphans no manifest knows about
        // (a restart mid-sync leaves downloaded files without a manifest). Only reached after a successful album fetch.
        store.DeleteStaleTempImages(staleTempAge);
        var imageIds = images.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var cachedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in store.ListCachedImageIds())
        {
            if (imageIds.Contains(id))
            {
                cachedIds.Add(id);
            }
            else
            {
                store.DeleteImage(id);
            }
        }

        var cacheSize = Math.Max(1, options.Value.CacheSize);
        var rotatePercent = Math.Clamp(options.Value.RotatePercent, 0, 100);

        // Hidden photos are never shown, so they must not use cache slots: evict them first and never refill with them.
        var hidden = store.LoadHidden();
        var evicted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in cachedIds.Where(hidden.Contains).ToList())
        {
            store.DeleteImage(id);
            cachedIds.Remove(id);
            evicted.Add(id);
        }

        var cached = images.Where(a => cachedIds.Contains(a.Id)).ToList();
        var uncached = images.Where(a => !cachedIds.Contains(a.Id) && !hidden.Contains(a.Id) && !evicted.Contains(a.Id)).ToList();

        if (cached.Count > cacheSize)
        {
            evictRandom(cached, cached.Count - cacheSize, evicted);
        }
        else if (cached.Count == cacheSize && rotatePercent > 0)
        {
            var rotateCount = Math.Max(1, cacheSize * rotatePercent / 100);
            if (uncached.Count >= rotateCount)
            {
                evictRandom(cached, rotateCount, evicted);
            }
        }

        var added = 0;
        var failed = 0;
        var maxFailures = cacheSize * 2;
        var candidates = uncached.ToArray();
        random.Shuffle(candidates);
        foreach (var asset in candidates)
        {
            if (cached.Count >= cacheSize || failed >= maxFailures)
            {
                break;
            }

            // Per-asset deadline: HttpClient.Timeout does not cover the body copy (ResponseHeadersRead),
            // so a silent mid-body stall would otherwise hang the worker forever.
            using var assetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            assetCts.CancelAfter(TimeSpan.FromSeconds(AssetTimeoutSeconds));
            try
            {
                await store.SaveImageAsync(asset.Id, stream => source.DownloadPreviewAsync(asset.Id, stream, assetCts.Token));
                entries[asset.Id] = await buildEntryAsync(asset, cancellationToken, assetCts.Token);
                cached.Add(asset);
                added++;
            }
            // A timeout surfaces as OperationCanceledException; only real shutdown (the outer token) should abort the pass.
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failed++;
                if (ex is HttpRequestException { StatusCode: { } status })
                {
                    logger.LogWarning(ex, "Skipping photo asset {AssetId}: HTTP {StatusCode}", asset.Id, status);
                }
                else
                {
                    logger.LogWarning(ex, "Skipping photo asset {AssetId}", asset.Id);
                }
            }
        }

        store.SaveManifest(entries.Values.ToList());
        library.Reload();
        logger.LogInformation(
            "Photo sync complete: {Indexed} album items, {Cached} photos cached ({Added} added, {Evicted} evicted), {Videos} videos",
            entries.Count,
            cached.Count,
            added,
            evicted.Count,
            videos);
        return true;
    }

    private void evictRandom(List<ImmichAsset> cached, int count, HashSet<string> evicted)
    {
        var pool = cached.ToArray();
        random.Shuffle(pool);
        foreach (var asset in pool.Take(count))
        {
            store.DeleteImage(asset.Id);
            evicted.Add(asset.Id);
            cached.Remove(asset);
        }
    }

    private async Task<PhotoEntry> buildEntryAsync(ImmichAsset asset, CancellationToken cancellationToken, CancellationToken assetToken)
    {
        IReadOnlyList<ImmichFace> faces;
        try
        {
            faces = await source.GetFacesAsync(asset.Id, assetToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "No face data for {AssetId}", asset.Id);
            faces = [];
        }

        var sized = faces.Where(f => f.ImageWidth > 0 && f.ImageHeight > 0).ToList();
        return new PhotoEntry
        {
            AssetId = asset.Id,
            TakenAt = asset.TakenAt,
            City = asset.City,
            State = asset.State,
            Country = asset.Country,
            ImageWidth = sized.Count > 0 ? sized[0].ImageWidth : 0,
            ImageHeight = sized.Count > 0 ? sized[0].ImageHeight : 0,
            Faces = sized
                .Select(f => new FaceBox(f.X1 / f.ImageWidth, f.Y1 / f.ImageHeight, f.X2 / f.ImageWidth, f.Y2 / f.ImageHeight))
                .ToList(),
        };
    }
}
