namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>An automatic playlist's songs, in order, as <see cref="SmartPlaylistRules"/> worked them out.</summary>
public sealed record SmartPlaylist(string Kind, string Name, IReadOnlyList<string> SongPaths);
