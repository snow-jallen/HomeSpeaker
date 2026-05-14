using System.Text.Json.Serialization;

namespace HomeSpeaker.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OfflineDownloadTargetStatus
{
    Ready,
    Missing
}
