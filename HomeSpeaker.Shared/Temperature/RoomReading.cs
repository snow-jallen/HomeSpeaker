namespace HomeSpeaker.Shared.Temperature;

#nullable enable

/// <summary>One room's sensor: its temperature and, when the device reports it, its battery level.</summary>
public sealed class RoomReading
{
    /// <summary>The room's configured name (or the device's name when no rooms are configured).</summary>
    public required string Name { get; init; }

    /// <summary>Null when the sensor could not be read (or no device matched this room).</summary>
    public double? Temperature { get; init; }

    /// <summary>Battery percent 0-100, or null when the sensor does not report one.</summary>
    public double? BatteryPercent { get; init; }
}
