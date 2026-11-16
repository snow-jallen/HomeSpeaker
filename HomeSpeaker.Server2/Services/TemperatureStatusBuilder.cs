using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Turns room readings (°F, in room order) into a <see cref="TemperatureStatus"/>: picks the outside and indoor rooms
/// and works out the window advice. Shared by every temperature source so they all answer the same way.
/// </summary>
public static class TemperatureStatusBuilder
{
    public static TemperatureStatus Build(IReadOnlyList<RoomReading> readings, TemperatureSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(settings);

        var status = new TemperatureStatus
        {
            ReadingTakenAt = now,
            Threshold = settings.ThresholdF,
            Rooms = [.. readings],
        };

        var outside = findRoom(readings, settings.OutsideRoom);
        status.OutsideRoomName = outside?.Name;
        status.OutsideTemperature = outside?.Temperature;

        if (settings.WindowAdvice)
        {
            // An unconfigured indoor room defaults to the first room that is not the outside one.
            var indoor = findRoom(readings, settings.IndoorRoom)
                ?? readings.FirstOrDefault(r => !string.Equals(r.Name, outside?.Name, StringComparison.OrdinalIgnoreCase));
            status.IndoorRoomName = indoor?.Name;
            status.IndoorTemperature = indoor?.Temperature;
        }

        if (status.OutsideTemperature is { } outsideTemp && status.IndoorTemperature is { } indoorTemp)
        {
            status.TemperatureDifference = Math.Abs(outsideTemp - indoorTemp);
            status.IsWithinThreshold = status.TemperatureDifference <= settings.ThresholdF;
            status.ShouldWindowsBeClosed = outsideTemp >= indoorTemp || outsideTemp < settings.WindowMinOutsideF;
        }

        return status;
    }

    private static RoomReading? findRoom(IReadOnlyList<RoomReading> readings, string? name) =>
        name is null ? null : readings.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
}
