namespace HomeSpeaker.Shared.Anchors;

/// <summary>
/// Create or edit a person. A null colour keeps the current one (or picks one for a new person). <c>FeedPersonId</c> links
/// this person to a person in the external feed: null keeps the link, empty removes it.
/// </summary>
public sealed record AnchorPersonRequest(string Name, string? Color = null, string? Emoji = null, string? FeedPersonId = null);
