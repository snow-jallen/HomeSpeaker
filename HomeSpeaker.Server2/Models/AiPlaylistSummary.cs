namespace HomeSpeaker.Server2.Models;

public class AiPlaylistSummary
{
    public string GenreKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class AiPlaylist
{
    public string GenreKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<AiPlaylistTrack> Tracks { get; set; } = new();
    public List<SongViewModel> Songs { get; set; } = new();
}

public class AiPlaylistTrack
{
    public required SongViewModel Song { get; set; }
    public double GenreScore { get; set; }
    public int GenreRank { get; set; }
    public string? Why { get; set; }
    public List<AiPlaylistTrackMarker> Markers { get; set; } = new();
}

public class AiPlaylistTrackMarker
{
    public string Key { get; set; } = string.Empty;
    public double Value { get; set; }
    public double Confidence { get; set; }
}

public class AiLibraryStatus
{
    public string State { get; set; } = "Idle";
    public int TotalTracks { get; set; }
    public int QueuedTracks { get; set; }
    public int ProcessingTracks { get; set; }
    public int CompletedTracks { get; set; }
    public int FailedTracks { get; set; }
    public double PercentComplete { get; set; }
    public DateTime? LastScanUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public string? CurrentBatchId { get; set; }
    public string? DegradedReason { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? LastFailureUtc { get; set; }
    public IReadOnlyList<string> ErrorDetails { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AiStatusActivity> RecentActivity { get; set; } = Array.Empty<AiStatusActivity>();
}

public class AiStatusActivity
{
    public DateTime TimestampUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? BatchId { get; set; }
}

public class AiPlayerContext
{
    public string? Mode { get; set; }
    public string? SessionId { get; set; }
    public string? GenreKey { get; set; }
    public int? SeedSongId { get; set; }
    public bool AllowFeedback { get; set; }
}

public class PlayerStatus
{
    public TimeSpan Elapsed { get; set; }
    public TimeSpan Remaining { get; set; }
    public bool StillPlaying { get; set; }
    public double PercentComplete { get; set; }
    public Song? CurrentSong { get; set; }
    public int Volume { get; set; }
    public bool IsStream { get; set; }
    public string? StreamName { get; set; }
    public AiPlayerContext? AiContext { get; set; }
}

public class Song
{
    public int SongId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
}
