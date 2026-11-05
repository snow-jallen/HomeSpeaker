using HomeSpeaker.Server2.Services.Anchors;

namespace HomeSpeaker.Server2.Data;

/// <summary>One of a person's anchors. Never deleted: retiring closes its active period so history keeps its meaning.</summary>
public class AnchorEntity
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public AnchorPersonEntity? Person { get; set; }
    public string Title { get; set; } = string.Empty;
    public AnchorSlot Slot { get; set; } = AnchorSlot.Day;
    public int SortOrder { get; set; }
    public TimeOnly? TargetTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<AnchorPeriodEntity> Periods { get; set; } = [];
    public List<AnchorLogEntity> Logs { get; set; } = [];
}
