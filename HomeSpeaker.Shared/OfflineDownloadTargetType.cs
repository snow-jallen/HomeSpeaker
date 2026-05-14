using System.Text.Json.Serialization;

namespace HomeSpeaker.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OfflineDownloadTargetType
{
    Artist,
    Album,
    Song
}
