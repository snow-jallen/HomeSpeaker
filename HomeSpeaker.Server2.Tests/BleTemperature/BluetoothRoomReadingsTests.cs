using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class BluetoothRoomReadingsTests
{
    private static readonly DateTime now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static SensorReading sensor(string mac, string name, double celsius, int battery, TimeSpan old) => new()
    {
        Mac = mac,
        Name = name,
        TemperatureC = celsius,
        Humidity = 50.5,
        BatteryPercent = battery,
        Rssi = -60,
        ObservedAt = now - old,
        Reporter = "kitchen",
    };

    private static readonly SensorReading boys = sensor("C9:38:30:34:17:76", "GVH5100_1776", 20, 100, TimeSpan.FromMinutes(1));
    private static readonly SensorReading emmalinda = sensor("C2:38:30:34:69:67", "GVH5100_6967", 25, 10, TimeSpan.FromMinutes(8));
    private static readonly SensorReading greenhouse = sensor("CA:38:30:34:5F:7E", "GVH5100_5F7E", 15, 96, TimeSpan.FromMinutes(25));

    private static IReadOnlyList<TemperatureDeviceMatcher.RoomPattern> rooms(string setting) => TemperatureRoomConfig.Parse(setting);

    [Fact]
    public void MapsSensorsToRoomsByMac_InRoomOrder()
    {
        var readings = BluetoothRoomReadings.Build(
            rooms("Emmalinda: C2:38:30:34:69:67; Boy's room: c9:38:30:34:17:76"),
            [boys, emmalinda],
            now);

        Assert.Equal(["Emmalinda", "Boy's room"], readings.Select(r => r.Name));
        Assert.Equal(77, readings[0].Temperature!.Value, 3);
        Assert.Equal(68, readings[1].Temperature!.Value, 3);
        Assert.Equal(10, readings[0].BatteryPercent);
        Assert.Equal(50.5, readings[1].Humidity);
        Assert.Equal(boys.ObservedAt, readings[1].LastReadingAt);
    }

    [Fact]
    public void RoomNobodyHears_HasNoReading()
    {
        var reading = Assert.Single(BluetoothRoomReadings.Build(rooms("Outside: C9:38:30:34:17:1C"), [boys], now));

        Assert.Equal("Outside", reading.Name);
        Assert.Null(reading.Temperature);
        Assert.Null(reading.Humidity);
        Assert.Null(reading.LastReadingAt);
        Assert.Null(reading.BatteryPercent);
    }

    [Fact]
    public void ReadingOverTwentyMinutesOld_DropsValuesButKeepsTimeAndBattery()
    {
        var reading = Assert.Single(BluetoothRoomReadings.Build(rooms("Greenhouse: CA:38:30:34:5F:7E"), [greenhouse], now));

        Assert.Null(reading.Temperature);
        Assert.Null(reading.Humidity);
        Assert.Equal(greenhouse.ObservedAt, reading.LastReadingAt);
        Assert.Equal(96, reading.BatteryPercent);
    }

    [Fact]
    public void ReadingFiveToTwentyMinutesOld_KeepsValues()
    {
        var reading = Assert.Single(BluetoothRoomReadings.Build(rooms("Emmalinda: C2:38:30:34:69:67"), [emmalinda], now));

        Assert.NotNull(reading.Temperature);
    }

    [Fact]
    public void NoRoomsConfigured_EverySensorIsItsOwnRoom_ByName()
    {
        var readings = BluetoothRoomReadings.Build([], [emmalinda, boys], now);

        Assert.Equal(["GVH5100_1776", "GVH5100_6967"], readings.Select(r => r.Name));
    }

    [Fact]
    public void UnmappedSensors_AreLeftOut_WhenRoomsAreConfigured() =>
        Assert.Single(BluetoothRoomReadings.Build(rooms("Boy's room: C9:38:30:34:17:76"), [boys, emmalinda, greenhouse], now));
}
