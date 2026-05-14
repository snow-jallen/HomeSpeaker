namespace HomeSpeaker.Shared;

public record OfflineDownloadTargetDto
{
    public int Id { get; init; }
    public OfflineDownloadTargetType TargetType { get; init; }
    public OfflineDownloadTargetStatus Status { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public string? SongPath { get; init; }
    public Song? Song { get; init; }
    public int ResolvedSongCount { get; init; }
    public DateTime CreatedUtc { get; init; }
}
