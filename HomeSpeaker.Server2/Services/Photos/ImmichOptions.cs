namespace HomeSpeaker.Server2.Services.Photos;

public class ImmichOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? AlbumId { get; set; }
    public int SyncIntervalMinutes { get; set; } = 15;
    public int SlideSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(AlbumId);
}
