namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>One row of play history, as <see cref="SmartPlaylistRules"/> sees it.</summary>
public sealed record PlayRecord(string SongPath, DateTime TimestampUtc, bool Skipped);
