using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>In-memory view of the photo cache. Thread-safe; registered as a singleton.</summary>
public sealed class PhotoLibrary
{
    private const int DefaultCacheSize = 40;

    private readonly PhotoCacheStore store;
    private readonly TimeProvider timeProvider;
    private readonly PhotoCycle cycle;
    private readonly Random random;
    private readonly int cacheSize;
    private readonly Lock gate = new();
    private Dictionary<string, PhotoEntry> photos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> videoLastServed = new(StringComparer.Ordinal);
    private long slidesServed;

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider, IOptions<ImmichOptions> options)
        : this(store, timeProvider, Random.Shared, options.Value.CacheSize)
    {
    }

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider, Random random, int cacheSize = DefaultCacheSize)
    {
        this.store = store;
        this.timeProvider = timeProvider;
        this.random = random;
        this.cacheSize = Math.Max(0, cacheSize);
        cycle = new PhotoCycle(random);
        Reload();
    }

    /// <summary>Raised (outside the lock) after Next() or Reload(), when the upcoming slide order may have changed.</summary>
    public event Action? UpcomingChanged;

    public int Count
    {
        get
        {
            lock (gate)
            {
                return photos.Count;
            }
        }
    }

    /// <summary>
    /// Rebuilds the visible set: every cached, non-hidden photo, plus a fresh random sample of the non-hidden
    /// videos sized to keep the album's video-to-photo ratio. Videos are never cached, so without sampling an
    /// album with many videos would drown out the photos.
    /// </summary>
    public void Reload()
    {
        var hidden = store.LoadHidden();
        var manifest = store.LoadManifest();
        var albumVideoCount = manifest.Count(p => p.IsVideo);
        var albumImageCount = manifest.Count - albumVideoCount;

        var visible = manifest
            .Where(p => !p.IsVideo && !hidden.Contains(p.AssetId) && store.HasImage(p.AssetId))
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var videos = manifest
            .Where(p => p.IsVideo && !hidden.Contains(p.AssetId))
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        lock (gate)
        {
            var sampleSize = Math.Min(videoSampleSize(visible.Count, albumImageCount, albumVideoCount), videos.Count);
            foreach (var video in sampleVideos(videos, sampleSize))
            {
                visible[video.AssetId] = video;
            }

            photos = visible;
            cycle.SetAvailable(visible.Keys);
            foreach (var id in videoLastServed.Keys.Where(id => !visible.ContainsKey(id)).ToList())
            {
                videoLastServed.Remove(id);
            }
        }

        UpcomingChanged?.Invoke();
    }

    /// <summary>
    /// What the video prefetcher needs, read under one lock: the videos among the next <paramref name="ahead"/> slides, every
    /// visible video, and the served-slide counter with the count at which each video was last served.
    /// </summary>
    public VideoPrefetchSnapshot GetVideoPrefetchSnapshot(int ahead)
    {
        lock (gate)
        {
            return new VideoPrefetchSnapshot(
                cycle.Peek(ahead).Where(id => photos.TryGetValue(id, out var entry) && entry.IsVideo).ToList(),
                photos.Values.Where(p => p.IsVideo).Select(p => p.AssetId).ToHashSet(StringComparer.Ordinal),
                slidesServed,
                new Dictionary<string, long>(videoLastServed, StringComparer.Ordinal));
        }
    }

    private int videoSampleSize(int visiblePhotos, int albumImageCount, int albumVideoCount)
    {
        if (visiblePhotos == 0 || albumImageCount == 0)
        {
            return Math.Min(albumVideoCount, cacheSize);
        }

        var proportional = (int)Math.Round((double)visiblePhotos * albumVideoCount / albumImageCount, MidpointRounding.AwayFromZero);
        return albumVideoCount > 0 ? Math.Max(1, proportional) : 0;
    }

    /// <summary>Partial Fisher-Yates: draws <paramref name="count"/> distinct videos. Call under the gate (Random is not thread-safe).</summary>
    private List<PhotoEntry> sampleVideos(List<PhotoEntry> videos, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var j = i + random.Next(videos.Count - i);
            (videos[i], videos[j]) = (videos[j], videos[i]);
        }

        return videos.GetRange(0, count);
    }

    public NextPhoto? Next()
    {
        PhotoEntry entry;
        lock (gate)
        {
            var id = cycle.Next();
            if (id is null)
            {
                return null;
            }

            entry = photos[id];
            slidesServed++;
            if (entry.IsVideo)
            {
                videoLastServed[id] = slidesServed;
            }
        }

        UpcomingChanged?.Invoke();

        var now = timeProvider.GetLocalNow();
        var today = new DateTime(now.Year, now.Month, now.Day);
        var layout = OverlayLayoutChooser.Choose(entry) == OverlayLayout.Mirrored ? "mirrored" : "default";
        var place = PhotoCaption.Place(entry);
        var when = PhotoCaption.When(entry.TakenAt, today);
        return entry.IsVideo
            ? new NextPhoto(entry.AssetId, $"/api/photos/{entry.AssetId}/video", place, when, "default", "video", entry.DurationMs)
            : new NextPhoto(entry.AssetId, $"/api/photos/{entry.AssetId}.jpg", place, when, layout, "photo", null);
    }

    /// <summary>True only for a currently visible (not hidden) video entry.</summary>
    public bool IsVideo(string assetId)
    {
        lock (gate)
        {
            return photos.TryGetValue(assetId, out var entry) && entry.IsVideo;
        }
    }

    public bool Hide(string assetId)
    {
        if (!PhotoCacheStore.IsValidAssetId(assetId))
        {
            return false;
        }

        lock (gate)
        {
            if (!photos.ContainsKey(assetId))
            {
                return false;
            }
        }

        store.AddHidden(assetId);
        Reload();
        return true;
    }
}
