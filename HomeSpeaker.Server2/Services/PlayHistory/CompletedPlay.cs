namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>One song that finished playing, one way or another, as worked out by <see cref="PlayTracker"/>.</summary>
public sealed record CompletedPlay(string SongPath, DateTimeOffset StartedAt, TimeSpan Heard, bool Skipped);
