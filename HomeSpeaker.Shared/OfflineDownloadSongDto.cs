namespace HomeSpeaker.Shared;

public record OfflineDownloadSongDto
{
    public Song Song { get; init; } = new();
    public string SongPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime LastModifiedUtc { get; init; }
    public string ETag { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public IReadOnlyList<OfflineDownloadSourceDto> Sources { get; init; } = [];
}
