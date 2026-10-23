using System.Text.Json;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// "Find my town" for the settings page: Open-Meteo's free geocoding API (no key). The typed client's base
/// address is <c>https://geocoding-api.open-meteo.com/</c>.
/// </summary>
public sealed class LocationSearchService(HttpClient httpClient)
{
    private const int MaxResults = 5;

    public async Task<IReadOnlyList<LocationMatch>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var uri = new Uri($"v1/search?name={Uri.EscapeDataString(query.Trim())}&count={MaxResults}", UriKind.Relative);
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var matches = new List<LocationMatch>();
        foreach (var result in results.EnumerateArray())
        {
            if (stringOf(result, "name") is not { } name ||
                !result.TryGetProperty("latitude", out var latitude) || latitude.ValueKind != JsonValueKind.Number ||
                !result.TryGetProperty("longitude", out var longitude) || longitude.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            matches.Add(new LocationMatch(name, stringOf(result, "admin1"), stringOf(result, "country"), latitude.GetDouble(), longitude.GetDouble()));
        }

        return matches;
    }

    private static string? stringOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
