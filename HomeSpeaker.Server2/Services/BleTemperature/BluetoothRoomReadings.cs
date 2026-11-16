using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Turns the hub's sensor readings into room readings (°F, like the cloud source) using the
/// <c>Temperature:BluetoothRooms</c> rules, whose patterns are sensor addresses.
/// </summary>
public static class BluetoothRoomReadings
{
    /// <summary>
    /// One reading per configured room, in order (empty rooms: every heard sensor is its own room, named after it).
    /// Readings past <see cref="ReadingAge.LostAfterMinutes"/> keep their time and battery but lose their values.
    /// </summary>
    public static IReadOnlyList<RoomReading> Build(
        IReadOnlyList<TemperatureDeviceMatcher.RoomPattern> rooms,
        IReadOnlyCollection<SensorReading> sensors,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(sensors);

        if (rooms.Count == 0)
        {
            return [.. sensors.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => toRoom(s.Name, s, now))];
        }

        return [.. rooms.Select(room => toRoom(room.Room, FindSensor(room, sensors), now))];
    }

    /// <summary>The sensor whose address is one of the room's patterns.</summary>
    public static SensorReading? FindSensor(TemperatureDeviceMatcher.RoomPattern room, IReadOnlyCollection<SensorReading> sensors) =>
        sensors.FirstOrDefault(s => room.Patterns.Any(p => string.Equals(p.Trim(), s.Mac, StringComparison.OrdinalIgnoreCase)));

    private static RoomReading toRoom(string name, SensorReading? sensor, DateTime now)
    {
        if (sensor is null)
        {
            return new RoomReading { Name = name };
        }

        var current = ReadingAge.Of(sensor.ObservedAt, now).ShowsValue;
        return new RoomReading
        {
            Name = name,
            Temperature = current ? (sensor.TemperatureC * 9 / 5) + 32 : null,
            Humidity = current ? sensor.Humidity : null,
            BatteryPercent = sensor.BatteryPercent,
            LastReadingAt = sensor.ObservedAt,
        };
    }
}
