using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Maps Govee devices to rooms by device-name patterns. Rooms are matched in the order given, and each
/// device can be claimed by only one room, so list the more specific patterns first (for example
/// "Emma" before "Girl", otherwise a device named "Emma's Girl Room" would answer for both).
/// </summary>
public static class TemperatureDeviceMatcher
{
    /// <summary>A room and the (case-insensitive) device-name fragments that identify its sensor.</summary>
    public sealed record RoomPattern(string Room, string[] Patterns);

    /// <returns>One entry per room, in the order given; the value is null when no unclaimed device matches.</returns>
    public static IReadOnlyDictionary<string, Device?> Match(IReadOnlyList<Device> devices, IReadOnlyList<RoomPattern> rooms)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<string, Device?>(StringComparer.Ordinal);

        foreach (var room in rooms)
        {
            var match = devices.FirstOrDefault(device =>
                !claimed.Contains(device.DeviceId) &&
                room.Patterns.Any(pattern => device.DeviceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

            if (match is not null)
            {
                claimed.Add(match.DeviceId);
            }

            result[room.Room] = match;
        }

        return result;
    }
}
