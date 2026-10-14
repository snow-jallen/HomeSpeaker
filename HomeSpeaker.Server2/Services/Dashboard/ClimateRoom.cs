using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>One indoor room tile on the Home climate card.</summary>
public sealed record ClimateRoom(string Label, double Temperature, bool LowBattery)
{
    /// <summary>A sensor battery below this percent gets a red battery icon.</summary>
    public const double LowBatteryPercent = 5;

    // Card order and the short labels (keys are the room names TemperatureService uses).
    private static readonly (string Key, string Label)[] indoor =
    [
        ("Downstairs", "Downstairs"),
        ("Greenhouse", "Greenhouse"),
        ("Boy", "Boys"),
        ("Girl", "Girls"),
        ("Emma", "Emma"),
    ];

    /// <summary>The indoor rooms that have a reading, in card order; rooms with no reading are left out.</summary>
    public static IReadOnlyList<ClimateRoom> From(TemperatureStatus? status)
    {
        if (status is null)
        {
            return [];
        }

        var rooms = new List<ClimateRoom>();
        foreach (var (key, label) in indoor)
        {
            var reading = status.Rooms.FirstOrDefault(r => string.Equals(r.Name, key, StringComparison.Ordinal));
            if (reading?.Temperature is { } temperature)
            {
                rooms.Add(new ClimateRoom(label, temperature, IsLow(reading.BatteryPercent)));
            }
        }

        return rooms;
    }

    public static bool IsLow(double? batteryPercent) => batteryPercent is < LowBatteryPercent;

    /// <summary>True when any sensor, hidden or not (outside included), reports a low battery.</summary>
    public static bool AnySensorLow(TemperatureStatus? status) =>
        status?.Rooms.Any(r => IsLow(r.BatteryPercent)) == true;
}
