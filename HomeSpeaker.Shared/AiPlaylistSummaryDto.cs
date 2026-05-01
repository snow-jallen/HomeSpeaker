namespace HomeSpeaker.Shared;

public record AiPlaylistSummaryDto
{
    public string GenreKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TrackCount { get; init; }
    public int SortOrder { get; init; }
    public DateTime? LastUpdatedUtc { get; init; }
}
