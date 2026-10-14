using System.Text.Json.Serialization;

namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// The Live Activity's <c>content-state</c>. These keys must match the iOS app's
/// <c>ActivityAttributes.ContentState</c> exactly, and every key is always sent.
/// </summary>
public sealed record LiveActivityContentState(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("isPlaying")] bool IsPlaying,
    [property: JsonPropertyName("isStream")] bool IsStream,
    [property: JsonPropertyName("volume")] int Volume,
    [property: JsonPropertyName("repeatMode")] bool RepeatMode,
    [property: JsonPropertyName("sleepTimerEndsAtUnix")] double? SleepTimerEndsAtUnix,
    [property: JsonPropertyName("updatedAtUnix")] double UpdatedAtUnix);
