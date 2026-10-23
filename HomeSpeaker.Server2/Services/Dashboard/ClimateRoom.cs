using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>One indoor room tile on the Home climate card.</summary>
public sealed record ClimateRoom(string Label, double Temperature, bool LowBattery)
{
    /// <summary>A sensor battery below this percent gets a red battery icon.</summary>
    public const double LowBatteryPercent = 5;

    /// <summary>
    /// Every configured room except the outside one, in configuration order and labelled with the room name;
    /// rooms with no reading are left out.
    /// </summary>
    public static IReadOnlyList<ClimateRoom> From(TemperatureStatus? status)
    {
        if (status is null)
        {
            return [];
        }

        var rooms = new List<ClimateRoom>();
        foreach (var reading in status.Rooms)
        {
            if (string.Equals(reading.Name, status.OutsideRoomName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (reading.Temperature is { } temperature)
            {
                rooms.Add(new ClimateRoom(reading.Name, temperature, IsLow(reading.BatteryPercent)));
            }
        }

        return rooms;
    }

    public static bool IsLow(double? batteryPercent) => batteryPercent is < LowBatteryPercent;

    /// <summary>True when any sensor, hidden or not (outside included), reports a low battery.</summary>
    public static bool AnySensorLow(TemperatureStatus? status) =>
        status?.Rooms.Any(r => IsLow(r.BatteryPercent)) == true;
}
