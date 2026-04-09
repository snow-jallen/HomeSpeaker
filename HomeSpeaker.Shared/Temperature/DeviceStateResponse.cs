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
    public bool? State => this.Capabilities?.FirstOrDefault(c => c.Instance == "online")?.State?.ValueAsBool;

    [JsonIgnore]
    public double? SensorTemperature => this.Capabilities?.FirstOrDefault(c => c.Instance == "sensorTemperature")?.State?.ValueAsDouble;

    [JsonIgnore]
    public double? SensorHumidity => this.Capabilities?.FirstOrDefault(c => c.Instance == "sensorHumidity")?.State?.ValueAsDouble;
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
    public double? ValueAsDouble => this.Value.ValueKind == JsonValueKind.Number && this.Value.TryGetDouble(out var d) ? d : null;

    [JsonIgnore]
    public bool? ValueAsBool => this.Value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
