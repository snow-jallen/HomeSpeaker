using HomeSpeaker.Server2.Data;

namespace HomeSpeaker.Server2.Services.PlaylistSuggestions;

/// <summary>A suggestion joined with the library song it points at, for the review page.</summary>
public sealed record SuggestedSong(
    int SuggestionId,
    SuggestionStatus Status,
    string? Reason,
    int SongId,
    string Name,
    string? Artist,
    string? Album,
    string Path);
