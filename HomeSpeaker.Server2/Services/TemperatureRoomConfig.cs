using System.Globalization;

namespace HomeSpeaker.Server2.Services;

/// <summary>Reads the temperature room, window-advice and threshold settings.</summary>
public static class TemperatureRoomConfig
{
    private const string DefaultOutsideRoom = "Outside";

    /// <summary>
    /// Parses <c>Room: pattern, pattern; Other: pattern</c>. Order is kept (first match wins) and an entry
    /// without a colon uses its name as its only pattern.
    /// </summary>
    public static IReadOnlyList<TemperatureDeviceMatcher.RoomPattern> Parse(string? value)
    {
        var rooms = new List<TemperatureDeviceMatcher.RoomPattern>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return rooms;
        }

        foreach (var entry in value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = entry.IndexOf(':', StringComparison.Ordinal);
            var name = (colon < 0 ? entry : entry[..colon]).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var patterns = colon < 0
                ? []
                : entry[(colon + 1)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            rooms.Add(new TemperatureDeviceMatcher.RoomPattern(name, patterns.Length == 0 ? [name] : patterns));
        }

        return rooms;
    }

    public static TemperatureSettings FromConfiguration(IConfiguration configuration)
    {
        var rooms = Parse(configuration["Temperature:Rooms"]);

        var outside = configuration["Temperature:OutsideRoom"]?.Trim();
        if (string.IsNullOrEmpty(outside))
        {
            outside = rooms.FirstOrDefault(r => string.Equals(r.Room, DefaultOutsideRoom, StringComparison.OrdinalIgnoreCase))?.Room;
        }

        var indoor = configuration["Temperature:IndoorRoom"]?.Trim();
        if (string.IsNullOrEmpty(indoor))
        {
            indoor = rooms.FirstOrDefault(r => !string.Equals(r.Room, outside, StringComparison.OrdinalIgnoreCase))?.Room;
        }

        var adviceOn = configuration.GetValue("Temperature:WindowAdvice", true);
        return new TemperatureSettings(
            rooms,
            outside,
            indoor,
            adviceOn && outside is not null,
            readDouble(configuration, "Temperature:WindowMinOutside", 50),
            readDouble(configuration, "TemperatureThreshold", 2));
    }

    private static double readDouble(IConfiguration configuration, string key, double fallback) =>
        double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
