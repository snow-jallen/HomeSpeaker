using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.BleTemperature;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class TemperatureSourceTests
{
    private static IConfiguration build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Theory]
    [InlineData(null, false)]
    [InlineData("cloud", false)]
    [InlineData("bluetooth", true)]
    [InlineData(" Bluetooth ", true)]
    public void IsBluetooth(string? source, bool expected) =>
        Assert.Equal(expected, TemperatureSource.IsBluetooth(build(new() { ["Temperature:Source"] = source })));

    [Fact]
    public void HubUrl_Empty_MeansThisSpeakerIsTheHub()
    {
        var config = build(new() { ["Temperature:Source"] = "bluetooth", ["Temperature:HubUrl"] = " " });

        Assert.Null(TemperatureSource.HubUrl(config));
        Assert.True(TemperatureSource.IsHub(config));
    }

    [Fact]
    public void HubUrl_GetsTrailingSlash()
    {
        var config = build(new() { ["Temperature:Source"] = "bluetooth", ["Temperature:HubUrl"] = "https://kitchen.example.ts.net" });

        Assert.Equal(new Uri("https://kitchen.example.ts.net/"), TemperatureSource.HubUrl(config));
        Assert.False(TemperatureSource.IsHub(config));
    }

    [Fact]
    public void HubUrl_NotHttp_IsIgnored() =>
        Assert.Null(TemperatureSource.HubUrl(build(new() { ["Temperature:HubUrl"] = "ftp://kitchen" })));

    [Fact]
    public void ScanTiming_Defaults() =>
        Assert.Equal((TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60)), TemperatureSource.ScanTiming(build(new())));

    [Fact]
    public void ScanTiming_BurstNeverLongerThanPeriod() =>
        Assert.Equal(
            (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)),
            TemperatureSource.ScanTiming(build(new() { ["Temperature:Bluetooth:ScanSeconds"] = "45", ["Temperature:Bluetooth:ScanEverySeconds"] = "30" })));

    [Fact]
    public void Rooms_ComeFromBluetoothRoomsKey_WhenSourceIsBluetooth()
    {
        var config = build(new()
        {
            ["Temperature:Source"] = "bluetooth",
            ["Temperature:Rooms"] = "Cloud room: H5179",
            ["Temperature:BluetoothRooms"] = "Outside: C9:38:30:34:17:1C; Kitchen: C9:38:30:34:17:76",
        });

        var settings = TemperatureRoomConfig.FromConfiguration(config);

        Assert.Equal(["Outside", "Kitchen"], settings.Rooms.Select(r => r.Room));
        Assert.Equal(["C9:38:30:34:17:1C"], settings.Rooms[0].Patterns);
        Assert.Equal("Outside", settings.OutsideRoom);
        Assert.Equal("Kitchen", settings.IndoorRoom);
    }

    [Fact]
    public void Rooms_ComeFromRoomsKey_ForCloud()
    {
        var config = build(new() { ["Temperature:Rooms"] = "Cloud room: H5179", ["Temperature:BluetoothRooms"] = "Kitchen: C9:38:30:34:17:76" });

        Assert.Equal(["Cloud room"], TemperatureRoomConfig.FromConfiguration(config).Rooms.Select(r => r.Room));
    }

    [Fact]
    public void ScanTiming_IsCappedAtAnHour() =>
        Assert.Equal(
            (TimeSpan.FromSeconds(10), TimeSpan.FromHours(1)),
            TemperatureSource.ScanTiming(build(new() { ["Temperature:Bluetooth:ScanEverySeconds"] = "5000000" })));

    [Theory]
    [InlineData("kitchen\n", "abc123", "kitchen")]
    [InlineData("  ", "abc123", "abc123")]
    [InlineData(null, "abc123", "abc123")]
    public void Reporter_PrefersTheHostsName(string? hostName, string machineName, string expected) =>
        Assert.Equal(expected, TemperatureSource.Reporter(hostName, machineName));
}
