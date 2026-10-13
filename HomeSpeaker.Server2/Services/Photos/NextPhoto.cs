namespace HomeSpeaker.Server2.Services.Photos;

/// <summary><c>Kind</c> is "photo" or "video"; <c>DurationMs</c> is only set for videos.</summary>
public sealed record NextPhoto(string Id, string Url, string? Place, string When, string Layout, string Kind, int? DurationMs);
