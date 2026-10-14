namespace HomeSpeaker.Shared;

/// <summary>
/// The player status served by <c>GET /api/homespeaker/player/status</c> and streamed by
/// <c>GET /api/homespeaker/player/events</c>.
/// </summary>
public record PlayerStatusDto
{
    public TimeSpan Elapsed { get; init; }
    public TimeSpan Remaining { get; init; }
    public bool StillPlaying { get; init; }
    public decimal PercentComplete { get; init; }

    /// <summary>
    /// The playing song. For a radio stream this is a synthetic song whose name is the stream's
    /// ICY "now playing" title (falling back to the station name) and whose path is the stream URL.
    /// </summary>
    public Song? CurrentSong { get; init; }
    public int Volume { get; init; }
    public bool SleepTimerActive { get; init; }
    public double? SleepTimerRemainingMinutes { get; init; }
    public bool RepeatMode { get; init; }
    public AiPlayerContextDto? AiContext { get; init; }
    public bool IsStream { get; init; }

    /// <summary>The radio station name the stream was started with (or its URL when unnamed).</summary>
    public string? StreamName { get; init; }
}
