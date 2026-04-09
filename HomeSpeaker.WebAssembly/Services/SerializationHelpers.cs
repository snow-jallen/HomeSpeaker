using System.Text.Json;

namespace HomeSpeaker.WebAssembly.Services;

internal static class SerializationHelpers
{

    public static readonly JsonSerializerOptions PropertyNameCaseInsensitive = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
}