namespace HomeSpeaker.Shared.Anchors;

/// <summary>
/// One person and their anchors. <c>Source</c> is <c>local</c> or <c>feed</c>; <c>TodayTotal</c> counts anchors expected today.
/// <c>FeedPersonId</c> is the external feed's person whose anchors are shown with this person's.
/// </summary>
public sealed record AnchorPersonDto(string Id, string Name, string Color, string? Emoji, string Source, int Streak, int TodayDone, int TodayTotal, List<AnchorDto> Anchors, string? FeedPersonId = null);
