namespace HomeSpeaker.Shared;

public record PlayerStatus
{
    public int Volume { get; init; }
    public decimal PercentComplete { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan Remaining { get; init; }
    public bool StillPlaying { get; init; }
    public bool IsStream { get; init; }
    public string? StreamName { get; init; }
    public Song? CurrentSong { get; init; }
}
