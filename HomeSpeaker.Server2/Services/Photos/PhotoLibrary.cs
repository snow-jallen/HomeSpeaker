namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>In-memory view of the photo cache. Thread-safe; registered as a singleton.</summary>
public sealed class PhotoLibrary
{
    private readonly PhotoCacheStore store;
    private readonly TimeProvider timeProvider;
    private readonly PhotoCycle cycle;
    private readonly Lock gate = new();
    private Dictionary<string, PhotoEntry> photos = new(StringComparer.Ordinal);

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider)
        : this(store, timeProvider, Random.Shared)
    {
    }

    public PhotoLibrary(PhotoCacheStore store, TimeProvider timeProvider, Random random)
    {
        this.store = store;
        this.timeProvider = timeProvider;
        cycle = new PhotoCycle(random);
        Reload();
    }

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

    public void Reload()
    {
        var hidden = store.LoadHidden();
        var visible = store.LoadManifest()
            .Where(p => !hidden.Contains(p.AssetId) && store.HasImage(p.AssetId))
            .GroupBy(p => p.AssetId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        lock (gate)
        {
            photos = visible;
            cycle.SetAvailable(visible.Keys);
        }
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
        }

        var today = timeProvider.GetLocalNow().UtcDateTime;
        var layout = OverlayLayoutChooser.Choose(entry) == OverlayLayout.Mirrored ? "mirrored" : "default";
        return new NextPhoto(entry.AssetId, $"/api/photos/{entry.AssetId}.jpg", PhotoCaption.Place(entry), PhotoCaption.When(entry.TakenAt, today), layout);
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
