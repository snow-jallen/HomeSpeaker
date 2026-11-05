namespace HomeSpeaker.Server2.Data;

/// <summary>Whether an anchor was done on a date. One per anchor and date; the newest client edit wins.</summary>
public class AnchorLogEntity
{
    public int Id { get; set; }
    public int AnchorId { get; set; }
    public DateOnly Date { get; set; }
    public bool Done { get; set; }
    public DateTimeOffset? DoneAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
