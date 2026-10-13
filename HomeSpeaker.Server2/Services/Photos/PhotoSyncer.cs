using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>One pass of mirroring the configured Immich album into the local photo cache.</summary>
public sealed class PhotoSyncer(
    IImmichClient immich,
    PhotoCacheStore store,
    PhotoLibrary library,
    IOptions<ImmichOptions> options,
    ILogger<PhotoSyncer> logger)
{
    private const int AssetTimeoutSeconds = 60;

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ImmichAsset> assets;
        try
        {
            assets = await immich.GetAlbumAssetsAsync(options.Value.AlbumId!, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (ex is HttpRequestException { StatusCode: { } status })
            {
                logger.LogWarning(ex, "Immich album fetch failed (HTTP {StatusCode}); keeping the existing photo cache", status);
            }
            else
            {
                logger.LogWarning(ex, "Immich album fetch failed; keeping the existing photo cache");
            }

            return false;
        }

        var existing = store.LoadManifest()
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var entries = new List<PhotoEntry>(assets.Count);
        var added = 0;

        foreach (var asset in assets)
        {
            if (asset.IsVideo)
            {
                // Videos are streamed from Immich on demand: no download, no face lookup, only a manifest entry.
                entries.Add(existing.TryGetValue(asset.Id, out var cachedVideo)
                    ? cachedVideo with { TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country, IsVideo = true, DurationMs = asset.DurationMs }
                    : new PhotoEntry
                    {
                        AssetId = asset.Id,
                        TakenAt = asset.TakenAt,
                        City = asset.City,
                        State = asset.State,
                        Country = asset.Country,
                        IsVideo = true,
                        DurationMs = asset.DurationMs,
                    });
                continue;
            }

            if (existing.TryGetValue(asset.Id, out var cached) && store.HasImage(asset.Id))
            {
                entries.Add(cached with { TakenAt = asset.TakenAt, City = asset.City, State = asset.State, Country = asset.Country });
                continue;
            }

            // Per-asset deadline: HttpClient.Timeout does not cover the body copy (ResponseHeadersRead),
            // so a silent mid-body stall would otherwise hang the worker forever.
            using var assetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            assetCts.CancelAfter(TimeSpan.FromSeconds(AssetTimeoutSeconds));
            try
            {
                await store.SaveImageAsync(asset.Id, stream => immich.DownloadPreviewAsync(asset.Id, stream, assetCts.Token));
                entries.Add(await buildEntryAsync(asset, cancellationToken, assetCts.Token));
                added++;
            }
            // A timeout surfaces as OperationCanceledException; only real shutdown (the outer token) should abort the pass.
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (ex is HttpRequestException { StatusCode: { } status })
                {
                    logger.LogWarning(ex, "Skipping Immich asset {AssetId}: HTTP {StatusCode}", asset.Id, status);
                }
                else
                {
                    logger.LogWarning(ex, "Skipping Immich asset {AssetId}", asset.Id);
                }
            }
        }

        var albumIds = assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var id in existing.Keys.Where(id => !albumIds.Contains(id)))
        {
            store.DeleteImage(id);
            removed++;
        }

        store.SaveManifest(entries);
        library.Reload();
        logger.LogInformation("Photo sync complete: {Total} photos, {Added} added, {Removed} removed", entries.Count, added, removed);
        return true;
    }

    private async Task<PhotoEntry> buildEntryAsync(ImmichAsset asset, CancellationToken cancellationToken, CancellationToken assetToken)
    {
        IReadOnlyList<ImmichFace> faces;
        try
        {
            faces = await immich.GetFacesAsync(asset.Id, assetToken);
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
