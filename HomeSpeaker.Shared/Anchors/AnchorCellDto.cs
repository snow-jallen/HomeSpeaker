namespace HomeSpeaker.Shared.Anchors;

/// <summary>How an anchor stands on a date: <c>State</c> is kept|missed|open|off|none|unknown. <c>Pending</c> means a change is still on its way to the anchor's source.</summary>
public sealed record AnchorCellDto(DateOnly Date, string State, bool Pending = false, DateTimeOffset? DoneAt = null, string? ActualTime = null);
