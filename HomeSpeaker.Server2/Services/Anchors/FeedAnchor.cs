namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// An anchor in an external feed (see docs/anchor-feed.md). Active every day in <c>[ActiveFrom, ActiveUntil)</c>.
/// <c>MissingIs</c> is <c>missed</c> (default) or <c>unknown</c>; <c>Writable</c> (default true) says whether HomeSpeaker may tick it.
/// </summary>
public sealed record FeedAnchor(
    string Id,
    string PersonId,
    string Title,
    string? Slot,
    int Order,
    string? TargetTime,
    DateOnly ActiveFrom,
    DateOnly? ActiveUntil,
    string? MissingIs,
    bool Writable = true);
