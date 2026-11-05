namespace HomeSpeaker.Server2.Data;

/// <summary>A tick on an external feed's anchor that hasn't reached the feed yet. One per anchor and date; a newer tick replaces it.</summary>
public class AnchorFeedOutboxEntity
{
    public int Id { get; set; }
    public string FeedAnchorId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public bool Done { get; set; }
    public DateTimeOffset ClientUpdatedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
