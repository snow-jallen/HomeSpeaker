namespace HomeSpeaker.Server2.Services.YouTube;

/// <summary>The videos a <see cref="YouTubeLink"/> points at; <paramref name="PlaylistTitle"/> is null for a single video.</summary>
public sealed record YouTubeLinkContents(string? PlaylistTitle, IReadOnlyList<VideoDto> Videos);
