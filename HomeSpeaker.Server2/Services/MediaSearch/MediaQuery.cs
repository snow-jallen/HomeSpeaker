using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.MediaSearch;

/// <summary>
/// A spoken request. Text is what to play; Kind is a hint when the request said what it was
/// ("the Hymns playlist"); Artist narrows songs and albums ("Because by the King's Singers").
/// </summary>
public sealed record MediaQuery(string Text, MediaKind? Kind = null, string? Artist = null);
