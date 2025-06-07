using System.Text.Json.Serialization;

using System.Collections.Generic;

namespace HomeSpeaker.Shared.Temperature;

#nullable enable

public sealed record DeviceResponse
{
    [JsonPropertyName("data")]
    public List<Device> Data { get; init; } = [];
}
