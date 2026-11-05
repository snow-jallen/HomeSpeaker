namespace HomeSpeaker.Shared.Anchors;

/// <summary>The full new order, as ids.</summary>
public sealed record AnchorOrderRequest(List<string> Ids);
