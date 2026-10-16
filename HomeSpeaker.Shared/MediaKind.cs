using System.Text.Json.Serialization;

namespace HomeSpeaker.Shared;

/// <summary>What a spoken "play ..." request resolved to. Declared in tie-break order.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MediaKind>))]
public enum MediaKind
{
    Playlist,
    Stream,
    Mix,
    Artist,
    Album,
    Song,
}
