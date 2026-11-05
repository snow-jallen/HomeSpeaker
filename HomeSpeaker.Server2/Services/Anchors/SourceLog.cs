namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>One day's entry for an anchor, from any source. <c>Pending</c>: a change still on its way to the anchor's source.</summary>
public sealed record SourceLog(bool Done, DateTimeOffset? DoneAt = null, string? ActualTime = null, bool Pending = false);
