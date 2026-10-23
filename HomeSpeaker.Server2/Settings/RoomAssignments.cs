using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// The logic behind the Govee room picker: it shows one row per sensor and saves <c>Temperature:Rooms</c> as
/// one exact device name per room, so rules written that way round-trip.
/// </summary>
public static class RoomAssignments
{
    private const string DefaultOutsideRoom = "Outside";

    /// <summary>
    /// Runs the current rules through <see cref="TemperatureDeviceMatcher"/>: matched devices come first in room
    /// order, then the unmatched ones (no room) in the order given.
    /// </summary>
    public static IReadOnlyList<RoomAssignment> FromRules(IReadOnlyList<string> deviceNames, string? roomsSetting, string? outsideRoom)
    {
        ArgumentNullException.ThrowIfNull(deviceNames);

        var devices = deviceNames
            .Select((name, index) => new Device { DeviceId = index.ToString(System.Globalization.CultureInfo.InvariantCulture), DeviceName = name, Sku = "" })
            .ToList();
        var matches = TemperatureDeviceMatcher.Match(devices, TemperatureRoomConfig.Parse(roomsSetting));

        var outside = string.IsNullOrWhiteSpace(outsideRoom)
            ? matches.Keys.FirstOrDefault(room => string.Equals(room, DefaultOutsideRoom, StringComparison.OrdinalIgnoreCase))
            : outsideRoom.Trim();

        var result = new List<RoomAssignment>();
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (room, device) in matches)
        {
            if (device is null)
            {
                continue;
            }

            assigned.Add(device.DeviceId);
            result.Add(new RoomAssignment(device.DeviceName, room, string.Equals(room, outside, StringComparison.OrdinalIgnoreCase)));
        }

        result.AddRange(devices.Where(d => !assigned.Contains(d.DeviceId)).Select(d => new RoomAssignment(d.DeviceName, null, false)));
        return result;
    }

    /// <summary><c>Room: device name</c> per named row, in order, and the outside row's room (null when none).</summary>
    public static (string Rooms, string? OutsideRoom) ToSettings(IReadOnlyList<RoomAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var named = assignments.Where(a => !string.IsNullOrWhiteSpace(a.Room)).ToList();
        var rooms = string.Join("; ", named.Select(a => $"{a.Room!.Trim()}: {a.DeviceName}"));
        var outside = named.FirstOrDefault(a => a.IsOutside)?.Room!.Trim();
        return (rooms, outside);
    }

    /// <summary>True when every rule has a single pattern that is exactly one of the device names.</summary>
    public static bool IsRepresentable(string? roomsSetting, IReadOnlyList<string> deviceNames)
    {
        ArgumentNullException.ThrowIfNull(deviceNames);

        var names = new HashSet<string>(deviceNames, StringComparer.Ordinal);
        return TemperatureRoomConfig.Parse(roomsSetting).All(rule => rule.Patterns.Length == 1 && names.Contains(rule.Patterns[0]));
    }
}
