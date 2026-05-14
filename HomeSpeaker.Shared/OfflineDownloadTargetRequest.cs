namespace HomeSpeaker.Shared;

public record OfflineDownloadTargetRequest
{
    public OfflineDownloadTargetType TargetType { get; init; }
    public int? SongId { get; init; }
    public string? SongPath { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
}
