using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.Temperature;

public sealed class TemperatureDeviceMatcherTests
{
    private static readonly TemperatureDeviceMatcher.RoomPattern[] rooms =
    [
        new("Outside", ["Outside"]),
        new("Emma", ["Emma"]),
        new("Boy", ["Boy"]),
        new("Girl", ["Girl"]),
    ];

    private static Device device(string id, string name) => new() { DeviceId = id, DeviceName = name, Sku = "H5179" };

    [Fact]
    public void Match_DeviceNamedForBothRooms_GoesToTheMoreSpecificRoomOnly()
    {
        var devices = new[] { device("1", "Emma's Girl Room") };

        var result = TemperatureDeviceMatcher.Match(devices, rooms);

        Assert.Equal("1", result["Emma"]?.DeviceId);
        Assert.Null(result["Girl"]);
    }

    [Fact]
    public void Match_SeparateDevices_EachRoomGetsItsOwn()
    {
        var devices = new[] { device("1", "Girls Room"), device("2", "Emma Room") };

        var result = TemperatureDeviceMatcher.Match(devices, rooms);

        Assert.Equal("2", result["Emma"]?.DeviceId);
        Assert.Equal("1", result["Girl"]?.DeviceId);
    }

    [Fact]
    public void Match_SpecificRoomClaimsFirstEvenWhenGirlDeviceListedFirst()
    {
        var devices = new[] { device("1", "Emma Girl"), device("2", "Little Girl") };

        var result = TemperatureDeviceMatcher.Match(devices, rooms);

        Assert.Equal("1", result["Emma"]?.DeviceId);
        Assert.Equal("2", result["Girl"]?.DeviceId);
    }

    [Fact]
    public void Match_NoMatchingDevice_IsNull()
    {
        var result = TemperatureDeviceMatcher.Match([device("1", "Garage")], rooms);

        Assert.All(rooms, room => Assert.Null(result[room.Room]));
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        var result = TemperatureDeviceMatcher.Match([device("1", "OUTSIDE thermo")], rooms);

        Assert.Equal("1", result["Outside"]?.DeviceId);
    }

    [Fact]
    public void Match_NoRooms_EachDeviceIsItsOwnRoom()
    {
        var devices = new[] { device("1", "Kitchen"), device("2", "Garage") };

        var result = TemperatureDeviceMatcher.Match(devices, []);

        Assert.Equal(["Kitchen", "Garage"], result.Keys);
        Assert.Equal("1", result["Kitchen"]?.DeviceId);
        Assert.Equal("2", result["Garage"]?.DeviceId);
    }
}
