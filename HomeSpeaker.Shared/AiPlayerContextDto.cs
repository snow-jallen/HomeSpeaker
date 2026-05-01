namespace HomeSpeaker.Shared;

public record AiPlayerContextDto
{
    public string? Mode { get; init; }
    public string? SessionId { get; init; }
    public string? GenreKey { get; init; }
    public int? SeedSongId { get; init; }
    public bool AllowFeedback { get; init; }
}
