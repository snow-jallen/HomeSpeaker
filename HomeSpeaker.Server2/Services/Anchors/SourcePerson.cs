namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>A person and their anchors from any mix of sources. <c>FeedPersonId</c> links a local person to a feed person.</summary>
public sealed record SourcePerson(
    string Id,
    string Name,
    string Color,
    string? Emoji,
    string Source,
    IReadOnlyList<SourceAnchor> Anchors,
    string? FeedPersonId = null);
