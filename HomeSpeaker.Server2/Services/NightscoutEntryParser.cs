using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Reads a NightScout entries array one entry at a time, so a single malformed entry is skipped instead of
/// failing the whole response. The time comes from the canonical <c>date</c> (epoch milliseconds, always UTC),
/// falling back to <c>dateString</c>, which uploaders write in several shapes.
/// </summary>
public static partial class NightscoutEntryParser
{
    /// <summary>Parses the entries, skipping bad ones. The result is in the order given; every date is UTC.</summary>
    public static List<BloodSugarReading> Parse(string json)
    {
        var readings = new List<BloodSugarReading>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return readings;
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (tryParseEntry(element) is { } reading)
            {
                readings.Add(reading);
            }
        }

        return readings;
    }

    private static BloodSugarReading? tryParseEntry(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("sgv", out var sgvElement) ||
                sgvElement.ValueKind != JsonValueKind.Number ||
                !sgvElement.TryGetDouble(out var sgv) ||
                tryReadTime(element) is not { } time)
            {
                return null;
            }

            return new BloodSugarReading
            {
                Sgv = sgv,
                DateString = time,
                Direction = readString(element, "direction"),
                Type = readString(element, "type"),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTime? tryReadTime(JsonElement element)
    {
        if (element.TryGetProperty("date", out var epoch) &&
            epoch.ValueKind == JsonValueKind.Number &&
            epoch.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        }

        return element.TryGetProperty("dateString", out var text) && text.ValueKind == JsonValueKind.String
            ? ParseDateString(text.GetString())
            : null;
    }

    /// <summary>Accepts ISO-8601 with Z, with +hh:mm, or with the Java-style +hhmm; no offset means UTC.</summary>
    public static DateTime? ParseDateString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalised = compactOffset().Replace(value.Trim(), "$1:$2");
        return DateTimeOffset.TryParse(normalised, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private static string readString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    [GeneratedRegex(@"([+-]\d{2})(\d{2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex compactOffset();
}
