using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Battery percent (0-100) when the state reports one, matching the capability instance case-insensitively on
    /// "battery" (for example "battery" or "batteryLevel"). Govee's developer API does not report it for every model.
    /// </summary>
    [JsonIgnore]
    public double? Battery => this.Capabilities?
        .FirstOrDefault(c => c.Instance?.Contains("battery", StringComparison.OrdinalIgnoreCase) == true)?
        .State?.ValueAsDouble;

    /// <summary>The capability instance names in this state, for diagnosing what a sensor reports.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> CapabilityInstances => this.Capabilities?
        .Select(c => c.Instance)
        .Where(i => !string.IsNullOrEmpty(i))
        .ToList() ?? [];

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
    public double? ValueAsDouble => this.Value.ValueKind switch
    {
        JsonValueKind.Number when this.Value.TryGetDouble(out var d) => d,
        JsonValueKind.String when double.TryParse(this.Value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };

    [JsonIgnore]
    public bool? ValueAsBool => this.Value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
