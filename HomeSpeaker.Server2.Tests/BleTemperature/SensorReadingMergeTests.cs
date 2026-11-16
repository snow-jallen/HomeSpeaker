using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class SensorReadingMergeTests
{
    private static readonly DateTime start = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static SensorReading reading(string reporter, int seconds, int rssi) => new()
    {
        Mac = "C9:38:30:34:17:76",
        Name = "GVH5100_1776",
        TemperatureC = 20,
        Humidity = 50,
        BatteryPercent = 100,
        Rssi = rssi,
        ObservedAt = start.AddSeconds(seconds),
        Reporter = reporter,
    };

    [Fact]
    public void NoCurrentReading_TakesIncoming() =>
        Assert.True(SensorReadingMerge.Prefers(null, reading("kitchen", 0, -80)));

    [Fact]
    public void MuchNewerReading_WinsDespiteWeakerSignal() =>
        Assert.True(SensorReadingMerge.Prefers(reading("kitchen", 0, -60), reading("rpi42", 61, -80)));

    [Fact]
    public void MuchOlderReading_Loses() =>
        Assert.False(SensorReadingMerge.Prefers(reading("kitchen", 61, -80), reading("rpi42", 0, -60)));

    [Fact]
    public void CloseInTime_StrongerSignalWins()
    {
        Assert.True(SensorReadingMerge.Prefers(reading("kitchen", 30, -80), reading("rpi42", 0, -60)));
        Assert.False(SensorReadingMerge.Prefers(reading("kitchen", 0, -60), reading("rpi42", 30, -80)));
    }

    [Fact]
    public void CloseInTime_EqualSignal_NewerWins()
    {
        Assert.True(SensorReadingMerge.Prefers(reading("kitchen", 0, -70), reading("rpi42", 10, -70)));
        Assert.False(SensorReadingMerge.Prefers(reading("kitchen", 10, -70), reading("rpi42", 0, -70)));
    }

    [Fact]
    public void SameReporter_NewerAlwaysWins() =>
        Assert.True(SensorReadingMerge.Prefers(reading("kitchen", 0, -60), reading("kitchen", 10, -90)));

    [Fact]
    public void SameReporter_OlderLoses() =>
        Assert.False(SensorReadingMerge.Prefers(reading("kitchen", 10, -90), reading("kitchen", 0, -60)));
}
