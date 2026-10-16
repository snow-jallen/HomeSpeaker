using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.MediaSearch;

/// <summary>Something "play ..." could pick. Key identifies it for playback (stream id, mix genre key, song id).</summary>
public sealed record MediaCandidate(MediaKind Kind, string Name, string Key, string? Artist = null);
