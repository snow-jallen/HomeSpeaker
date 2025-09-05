using System;

namespace HomeSpeaker.Shared;

public record PlayerStatus
{
    public decimal PercentComplete { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan Remaining { get; init; }
    public bool StillPlaying { get; init; }
    public Song CurrentSong { get; init; }
}
