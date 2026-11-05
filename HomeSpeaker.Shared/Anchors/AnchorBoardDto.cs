namespace HomeSpeaker.Shared.Anchors;

/// <summary>
/// Everyone's anchors over a date range. <c>Today</c> is the anchor home's local date. <c>Stale</c> means the anchor home
/// (another HomeSpeaker) didn't answer and this is the last board it sent.
/// </summary>
public sealed record AnchorBoardDto(DateOnly Today, DateOnly From, DateOnly To, List<AnchorPersonDto> People, AnchorFeedStatusDto? Feed = null, bool Stale = false);
