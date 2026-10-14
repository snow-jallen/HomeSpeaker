using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class ClimateCardTests
{
    private static RoomReading room(string name, double? temperature, double? battery = null) =>
        new() { Name = name, Temperature = temperature, BatteryPercent = battery };

    [Fact]
    public void Rooms_AreInCardOrder_WithShortLabels_AndHideMissingReadings()
    {
        var status = new TemperatureStatus
        {
            Rooms =
            [
                room("Outside", 50),
                room("Girl", 67),
                room("Emma", 68),
                room("Boy", null),
                room("Greenhouse", 80),
                room("Downstairs", 70),
            ],
        };

        var rooms = ClimateRoom.From(status);

        Assert.Equal(["Downstairs", "Greenhouse", "Girls", "Emma"], rooms.Select(r => r.Label));
        Assert.Equal([70, 80, 67, 68], rooms.Select(r => r.Temperature));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(100.0, false)]
    [InlineData(5.0, false)]
    [InlineData(4.9, true)]
    [InlineData(0.0, true)]
    public void LowBattery_OnlyBelowFivePercent(double? battery, bool expectedLow)
    {
        var rooms = ClimateRoom.From(new TemperatureStatus { Rooms = [room("Boy", 70, battery)] });

        Assert.Equal(expectedLow, Assert.Single(rooms).LowBattery);
    }

    [Fact]
    public void AnySensorLow_SeesHiddenAndOutsideSensors()
    {
        Assert.True(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Boy", null, 2)] }));
        Assert.True(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Outside", 50, 1)] }));
        Assert.False(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Boy", 70, 50)] }));
        Assert.False(ClimateRoom.AnySensorLow(null));
    }

    [Fact]
    public void Rooms_NullStatus_IsEmpty() => Assert.Empty(ClimateRoom.From(null));

    private static TemperatureStatus advice(double outside, double girls, bool shouldClose) => new()
    {
        OutsideTemperature = outside,
        YoungerGirlsRoomTemperature = girls,
        TemperatureDifference = Math.Abs(outside - girls),
        ShouldWindowsBeClosed = shouldClose,
    };

    [Fact]
    public void WindowAdvice_CloseBecauseItIsWarmerOutside()
    {
        var result = WindowAdvice.From(advice(80, 70, shouldClose: true));

        Assert.True(result!.Close);
        Assert.Equal("Close windows · 10° warmer outside", result.Text);
    }

    [Fact]
    public void WindowAdvice_CloseBecauseItIsTooCold()
    {
        var result = WindowAdvice.From(advice(42, 67, shouldClose: true));

        Assert.True(result!.Close);
        Assert.Equal("Close windows · 25° colder outside", result.Text);
    }

    [Fact]
    public void WindowAdvice_CloseWhenTemperaturesAreEqual_SaysSame()
    {
        var result = WindowAdvice.From(advice(70, 70, shouldClose: true));

        Assert.True(result!.Close);
        Assert.Equal("Close windows · same as inside", result.Text);
    }

    [Fact]
    public void WindowAdvice_OpenWhenCoolerOutside()
    {
        var result = WindowAdvice.From(advice(60, 70, shouldClose: false));

        Assert.False(result!.Close);
        Assert.Equal("Open windows · 10° cooler outside", result.Text);
    }

    [Fact]
    public void WindowAdvice_DropsTheNumberWhenItRoundsToZero()
    {
        Assert.Equal("Open windows · cooler outside", WindowAdvice.From(advice(69.8, 70, shouldClose: false))!.Text);
    }

    [Fact]
    public void WindowAdvice_NoneWhenAnInputIsMissing()
    {
        Assert.Null(WindowAdvice.From(null));
        Assert.Null(WindowAdvice.From(new TemperatureStatus { OutsideTemperature = 60 }));
        Assert.Null(WindowAdvice.From(new TemperatureStatus { YoungerGirlsRoomTemperature = 70 }));
    }
}
