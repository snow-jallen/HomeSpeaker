namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>A stretch of dates an anchor is active, <c>[From, To)</c>, and the weekdays it is scheduled on.</summary>
public sealed record AnchorPeriod(DateOnly From, DateOnly? To, int Days)
{
    public bool IsOpen => To is null;

    public bool Covers(DateOnly date) => From <= date && (To is null || date < To);

    public bool IsScheduled(DateOnly date) => Covers(date) && DayMask.Includes(Days, date.DayOfWeek);
}
