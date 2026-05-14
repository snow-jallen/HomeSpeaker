namespace HomeSpeaker.Shared;

public record OfflineDownloadSourceDto
{
    public int TargetId { get; init; }
    public OfflineDownloadTargetType TargetType { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
