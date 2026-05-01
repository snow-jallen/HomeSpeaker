namespace HomeSpeaker.Shared;

public record AiLibraryStatusDto
{
    public string State { get; init; } = "Idle";
    public int TotalTracks { get; init; }
    public int QueuedTracks { get; init; }
    public int ProcessingTracks { get; init; }
    public int CompletedTracks { get; init; }
    public int FailedTracks { get; init; }
    public double PercentComplete { get; init; }
    public DateTime? LastScanUtc { get; init; }
    public DateTime? LastHeartbeatUtc { get; init; }
    public string? CurrentBatchId { get; init; }
    public string? DegradedReason { get; init; }
    public string? LastErrorMessage { get; init; }
    public DateTime? LastFailureUtc { get; init; }
    public IReadOnlyList<string> ErrorDetails { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AiStatusActivityDto> RecentActivity { get; init; } = Array.Empty<AiStatusActivityDto>();
}
