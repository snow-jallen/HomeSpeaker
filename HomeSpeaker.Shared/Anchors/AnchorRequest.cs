namespace HomeSpeaker.Shared.Anchors;

/// <summary>Create or edit an anchor. Null fields keep their current value (defaults for a new anchor: slot day, every day). <c>TargetTime</c> is HH:mm, or empty to clear it.</summary>
public sealed record AnchorRequest(string Title, string? Slot = null, string? TargetTime = null, int? Days = null);
