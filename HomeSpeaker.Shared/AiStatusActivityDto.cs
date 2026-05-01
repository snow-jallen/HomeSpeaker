namespace HomeSpeaker.Shared;

public record AiStatusActivityDto
{
    public DateTime TimestampUtc { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? BatchId { get; init; }
}
