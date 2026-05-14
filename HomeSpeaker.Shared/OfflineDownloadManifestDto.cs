namespace HomeSpeaker.Shared;

public record OfflineDownloadManifestDto
{
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<OfflineDownloadTargetDto> Targets { get; init; } = [];
    public IReadOnlyList<OfflineDownloadSongDto> Songs { get; init; } = [];
}
