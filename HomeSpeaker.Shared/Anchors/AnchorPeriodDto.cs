namespace HomeSpeaker.Shared.Anchors;

/// <summary>A stretch of dates an anchor is active, <c>[From, To)</c>.</summary>
public sealed record AnchorPeriodDto(DateOnly From, DateOnly? To, int Days);
