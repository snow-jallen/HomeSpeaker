namespace HomeSpeaker.Shared.Anchors;

/// <summary>
/// The external feed's health: <c>Status</c> is ok|stale|unreachable. <c>People</c> lists the feed's people, for linking
/// one to a person here.
/// </summary>
public sealed record AnchorFeedStatusDto(string Name, string Status, DateTimeOffset? AsOf = null, string? Error = null, List<AnchorFeedPersonDto>? People = null);
