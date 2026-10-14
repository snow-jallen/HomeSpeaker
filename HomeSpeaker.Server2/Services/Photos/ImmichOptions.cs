namespace HomeSpeaker.Server2.Services.Photos;

public class ImmichOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? AlbumId { get; set; }
    public int SyncIntervalMinutes { get; set; } = 15;
    public int SlideSeconds { get; set; } = 30;
    public int MaxVideoSeconds { get; set; } = 60;

    /// <summary>How many photos to keep on disk. The rest of the album is indexed but not downloaded.</summary>
    public int CacheSize { get; set; } = 40;

    /// <summary>Percent of a full cache replaced with fresh photos on each sync (0 disables rotation).</summary>
    public int RotatePercent { get; set; } = 25;

    /// <summary>How many upcoming slides to look ahead when pre-downloading videos to the Pi (0 disables prefetching).</summary>
    public int VideoPrefetchAhead { get; set; } = 5;

    /// <summary>Videos larger than this many MB are not prefetched; they keep streaming live from Immich.</summary>
    public int VideoPrefetchMaxMB { get; set; } = 300;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(AlbumId);
}
