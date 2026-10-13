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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(AlbumId);
}
