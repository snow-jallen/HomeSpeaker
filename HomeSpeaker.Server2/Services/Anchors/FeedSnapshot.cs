using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>The feed's anchors (null when it has never answered) and how fresh they are.</summary>
public sealed record FeedSnapshot(FeedPayload? Payload, AnchorFeedStatusDto Status);
