namespace HomeSpeaker.Shared.Anchors;

/// <summary>Mark an anchor done or not done on a date. <c>ClientUpdatedAt</c> is when the person tapped; the newest tap wins.</summary>
public sealed record AnchorLogRequest(bool Done, DateTimeOffset? ClientUpdatedAt = null);
