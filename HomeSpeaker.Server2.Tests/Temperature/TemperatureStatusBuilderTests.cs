using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.Temperature;

public sealed class TemperatureStatusBuilderTests
{
    private static readonly DateTime now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static TemperatureSettings settings(string? indoor = "Kitchen") =>
        new(TemperatureRoomConfig.Parse("Outside: a; Kitchen: b; Bedroom: c"), "Outside", indoor, WindowAdvice: true, WindowMinOutsideF: 50, ThresholdF: 2);

    [Fact]
    public void WarmerOutside_SaysCloseWindows()
    {
        var status = TemperatureStatusBuilder.Build(
            [new RoomReading { Name = "Outside", Temperature = 75 }, new RoomReading { Name = "Kitchen", Temperature = 70 }],
            settings(),
            now);

        Assert.Equal(75, status.OutsideTemperature);
        Assert.Equal("Kitchen", status.IndoorRoomName);
        Assert.Equal(70, status.IndoorTemperature);
        Assert.True(status.ShouldWindowsBeClosed);
        Assert.Equal(5, status.TemperatureDifference);
        Assert.Equal(now, status.ReadingTakenAt);
    }

    [Fact]
    public void IndoorRoomWithoutReading_GivesNoAdvice()
    {
        var status = TemperatureStatusBuilder.Build(
            [new RoomReading { Name = "Outside", Temperature = 60 }, new RoomReading { Name = "Kitchen", LastReadingAt = now.AddHours(-1) }],
            settings(),
            now);

        Assert.Null(status.IndoorTemperature);
        Assert.Equal(0, status.TemperatureDifference);
        Assert.False(status.ShouldWindowsBeClosed);
    }
}
