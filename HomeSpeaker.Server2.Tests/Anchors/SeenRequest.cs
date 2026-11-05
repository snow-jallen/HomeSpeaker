namespace HomeSpeaker.Server2.Tests.Anchors;

/// <summary>A request <see cref="FakeFeedServer"/> received.</summary>
internal sealed record SeenRequest(string Method, string Uri, string? Authorization, string Body);
