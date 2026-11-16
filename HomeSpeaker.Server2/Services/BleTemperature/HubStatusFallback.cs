using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// What a non-hub speaker shows while the hub is unreachable: the hub's last status, with every value judged again
/// by <see cref="ReadingAge"/> at <c>now</c>, so rooms keep the hub's names and order, and values age out on schedule.
/// Window advice survives only while both of its rooms still have a value.
/// </summary>
public static class HubStatusFallback
{
    public static TemperatureStatus Age(TemperatureStatus last, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(last);

        var rooms = last.Rooms.Select(room => ReadingAge.Of(room.LastReadingAt, now).ShowsValue
            ? room
            : new RoomReading { Name = room.Name, BatteryPercent = room.BatteryPercent, LastReadingAt = room.LastReadingAt }).ToList();

        double? valueOf(string? name) =>
            name is null ? null : rooms.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))?.Temperature;

        var outside = valueOf(last.OutsideRoomName);
        var indoor = valueOf(last.IndoorRoomName);
        var adviceHolds = outside is not null && indoor is not null;
        return new TemperatureStatus
        {
            Rooms = rooms,
            OutsideRoomName = last.OutsideRoomName,
            OutsideTemperature = outside,
            IndoorRoomName = last.IndoorRoomName,
            IndoorTemperature = indoor,
            ShouldWindowsBeClosed = adviceHolds && last.ShouldWindowsBeClosed,
            TemperatureDifference = adviceHolds ? last.TemperatureDifference : 0,
            IsWithinThreshold = adviceHolds && last.IsWithinThreshold,
            Threshold = last.Threshold,
            ReadingTakenAt = last.ReadingTakenAt,
            LastCachedAt = last.LastCachedAt,
            ShowsReadingAge = last.ShowsReadingAge,
        };
    }
}
