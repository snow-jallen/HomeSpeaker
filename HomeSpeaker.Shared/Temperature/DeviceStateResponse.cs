using System.Text.Json;
using System.Text.Json.Serialization;

using System.Linq;

namespace HomeSpeaker.Shared.Temperature;

#nullable enable

public sealed record DeviceStateResponse(
    [property: JsonPropertyName("payload")] DeviceStatePayload Payload
);

public sealed record DeviceStatePayload(
    [property: JsonPropertyName("capabilities")] DeviceCapability[] Capabilities
)
{
    [JsonIgnore]
    public bool? State => Capabilities?.FirstOrDefault(c => c.Instance == "online")?.State?.ValueAsBool;

    [JsonIgnore]
    public double? SensorTemperature => Capabilities?.FirstOrDefault(c => c.Instance == "sensorTemperature")?.State?.ValueAsDouble;

    [JsonIgnore]
    public double? SensorHumidity => Capabilities?.FirstOrDefault(c => c.Instance == "sensorHumidity")?.State?.ValueAsDouble;
}

public sealed record DeviceCapability(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("state")] DeviceCapabilityState State
);

public sealed record DeviceCapabilityState(
    [property: JsonPropertyName("value")] JsonElement Value
)
{
    [JsonIgnore]
    public double? ValueAsDouble => Value.ValueKind == JsonValueKind.Number && Value.TryGetDouble(out var d) ? d : null;

    [JsonIgnore]
    public bool? ValueAsBool => Value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
