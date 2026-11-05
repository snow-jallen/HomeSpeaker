namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>What <c>GET {feed}/anchors?from&amp;to</c> returns (see docs/anchor-feed.md).</summary>
public sealed record FeedPayload(List<FeedPerson> People, List<FeedAnchor> Anchors, List<FeedLog> Logs);
