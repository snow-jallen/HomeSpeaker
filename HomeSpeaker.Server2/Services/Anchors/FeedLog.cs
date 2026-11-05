namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>One day's entry for a feed anchor (see docs/anchor-feed.md). <c>ActualTime</c> is HH:mm, e.g. when someone slept.</summary>
public sealed record FeedLog(string AnchorId, DateOnly Date, bool Done, DateTimeOffset? DoneAt, string? ActualTime);
