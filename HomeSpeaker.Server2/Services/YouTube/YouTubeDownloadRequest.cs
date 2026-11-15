namespace HomeSpeaker.Server2.Services.YouTube;

/// <summary>
/// One video to save as an MP3. <paramref name="Collection"/> is the playlist title: the songs go in a
/// folder of that name under "YouTube Cache" and get it as their album, numbered by <paramref name="TrackNumber"/>.
/// </summary>
public sealed record YouTubeDownloadRequest(string VideoId, string Title, string? Channel, string? Collection = null, int? TrackNumber = null);
