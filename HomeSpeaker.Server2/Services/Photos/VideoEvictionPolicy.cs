namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Decides which cached videos to delete: one that is no longer visible, or one that is outside the look-ahead window
/// and has had <see cref="SlidesBeforeEviction"/> more slides served since it was last served. A video that was
/// prefetched but never served (the order changed under it) is aged from the moment the policy first saw it.
/// Not thread-safe; the prefetcher is its only caller.
/// </summary>
public sealed class VideoEvictionPolicy
{
    public const int SlidesBeforeEviction = 10;

    private readonly Dictionary<string, long> firstSeen = new(StringComparer.Ordinal);

    public IReadOnlyList<string> SelectEvictions(IReadOnlyCollection<string> cachedIds, VideoPrefetchSnapshot snapshot)
    {
        foreach (var gone in firstSeen.Keys.Where(id => !cachedIds.Contains(id, StringComparer.Ordinal)).ToList())
        {
            firstSeen.Remove(gone);
        }

        var evict = new List<string>();
        foreach (var id in cachedIds)
        {
            firstSeen.TryAdd(id, snapshot.SlidesServed);

            if (!snapshot.VisibleVideos.Contains(id))
            {
                evict.Add(id);
            }
            else if (!snapshot.UpcomingVideos.Contains(id, StringComparer.Ordinal))
            {
                var reference = snapshot.LastServed.TryGetValue(id, out var served) ? served : firstSeen[id];
                if (snapshot.SlidesServed - reference >= SlidesBeforeEviction)
                {
                    evict.Add(id);
                }
            }
        }

        // An evicted video that is downloaded again later starts a fresh age.
        foreach (var id in evict)
        {
            firstSeen.Remove(id);
        }

        return evict;
    }
}
