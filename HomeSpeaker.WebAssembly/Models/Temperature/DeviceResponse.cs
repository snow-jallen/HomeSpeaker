using System.Text.Json.Serialization;
using HomeSpeaker.WebAssembly.Models.Temperature;

namespace HomeSpeaker.WebAssembly.Models.Temperature;

public sealed record DeviceResponse
{
    [JsonPropertyName("data")]
    public List<Device> Data { get; init; } = [];
}
