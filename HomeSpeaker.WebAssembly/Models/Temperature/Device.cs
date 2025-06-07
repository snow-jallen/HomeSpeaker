using System.Text.Json.Serialization;

namespace HomeSpeaker.WebAssembly.Models.Temperature;

public record Device
{
    [JsonPropertyName("device")]
    public required string DeviceId { get; init; }

    [JsonPropertyName("deviceName")]
    public required string DeviceName { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("capabilities")]
    public List<Capability> Capabilities { get; init; } = [];
}

public record Capability
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("instance")]
    public string? Instance { get; init; }
}
