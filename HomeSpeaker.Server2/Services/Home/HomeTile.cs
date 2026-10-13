namespace HomeSpeaker.Server2.Services.Home;

/// <summary>A device or scene shown on the Home page's Home row. State is display text such as "on", "off" or "68°".</summary>
public sealed record HomeTile(string Id, string Name, HomeTileKind Kind, string? State);
