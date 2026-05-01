namespace HomeSpeaker.Shared;

public record AiFeedbackRequest
{
    public string? SessionId { get; init; }
    public int SongId { get; init; }
    public string Feedback { get; init; } = string.Empty;
    public int? PreviousSongId { get; init; }
}
