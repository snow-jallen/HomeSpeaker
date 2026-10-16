namespace HomeSpeaker.Shared;

/// <summary>The library item a "play ..." search picked.</summary>
public record MediaMatchDto(MediaKind Kind, string Name, string? Artist);
