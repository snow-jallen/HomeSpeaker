namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>A consistent view of the slideshow for the video prefetcher.</summary>
/// <param name="UpcomingVideos">Videos among the next N slides, in the order they will be shown.</param>
/// <param name="VisibleVideos">Every video currently in the visible set.</param>
/// <param name="SlidesServed">How many slides Next() has handed out so far.</param>
/// <param name="LastServed">For each video served so far, the value of <paramref name="SlidesServed"/> right after it was served.</param>
public sealed record VideoPrefetchSnapshot(
    IReadOnlyList<string> UpcomingVideos,
    IReadOnlySet<string> VisibleVideos,
    long SlidesServed,
    IReadOnlyDictionary<string, long> LastServed);
