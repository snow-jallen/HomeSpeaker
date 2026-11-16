using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// One room tile on the Home climate card. <c>Note</c> says how old the reading is ("7 min old",
/// "No reading for 34 min") for sources that track it; <c>Temperature</c> is null when there's no current value.
/// </summary>
public sealed record ClimateRoom(string Label, double? Temperature, bool LowBattery, string? Note = null)
{
    /// <summary>A sensor battery below this percent gets a red battery icon.</summary>
    public const double LowBatteryPercent = 5;

    /// <summary>
    /// Every configured room except the outside one, in configuration order and labelled with the room name. Rooms
    /// with no reading are left out, unless the source tracks reading age (Bluetooth): then every room is listed with
    /// its age, judged at <paramref name="now"/> so a value that aged out since it was fetched is hidden.
    /// </summary>
    public static IReadOnlyList<ClimateRoom> From(TemperatureStatus? status, DateTime now)
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

            if (status.ShowsReadingAge)
            {
                rooms.Add(withAge(reading, now));
            }
            else if (reading.Temperature is { } temperature)
            {
                rooms.Add(new ClimateRoom(reading.Name, temperature, IsLow(reading.BatteryPercent)));
            }
        }

        return rooms;
    }

    /// <summary>The outside room as a tile (its age note included where tracked), or null when there is none.</summary>
    public static ClimateRoom? Outside(TemperatureStatus? status, DateTime now)
    {
        var reading = status?.Rooms.FirstOrDefault(r => string.Equals(r.Name, status.OutsideRoomName, StringComparison.OrdinalIgnoreCase));
        if (reading is null)
        {
            return null;
        }

        return status!.ShowsReadingAge ? withAge(reading, now) : new ClimateRoom(reading.Name, reading.Temperature, IsLow(reading.BatteryPercent));
    }

    public static bool IsLow(double? batteryPercent) => batteryPercent is < LowBatteryPercent;

    /// <summary>True when any sensor, hidden or not (outside included), reports a low battery.</summary>
    public static bool AnySensorLow(TemperatureStatus? status) =>
        status?.Rooms.Any(r => IsLow(r.BatteryPercent)) == true;

    private static ClimateRoom withAge(RoomReading reading, DateTime now)
    {
        var age = ReadingAge.Of(reading.LastReadingAt, now);
        return new ClimateRoom(reading.Name, age.ShowsValue ? reading.Temperature : null, IsLow(reading.BatteryPercent), age.Note);
    }
}
