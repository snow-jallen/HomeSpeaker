namespace HomeSpeaker.Server2.Data;

/// <summary>A stretch of dates an anchor is active, <c>[From, To)</c>, and the weekdays it is scheduled on.</summary>
public class AnchorPeriodEntity
{
    public int Id { get; set; }
    public int AnchorId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly? To { get; set; }
    public int Days { get; set; }
}
