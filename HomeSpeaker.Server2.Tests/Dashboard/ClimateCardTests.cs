using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class ClimateCardTests
{
    private static RoomReading room(string name, double? temperature, double? battery = null) =>
        new() { Name = name, Temperature = temperature, BatteryPercent = battery };

    [Fact]
    public void ClimateRoom_From_ListsConfiguredIndoorRoomsInOrder()
    {
        var status = new TemperatureStatus
        {
            OutsideRoomName = "Outside",
            Rooms =
            [
                room("Outside", 50),
                room("Greenhouse", 80),
                room("Emma", 68),
                room("Boys", null),
                room("Downstairs", 70),
                room("Girls", 67),
            ],
        };

        var rooms = ClimateRoom.From(status, DateTime.UtcNow);

        Assert.Equal(["Greenhouse", "Emma", "Downstairs", "Girls"], rooms.Select(r => r.Label));
        Assert.Equal(new double?[] { 80, 68, 70, 67 }, rooms.Select(r => r.Temperature));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(100.0, false)]
    [InlineData(5.0, false)]
    [InlineData(4.9, true)]
    [InlineData(0.0, true)]
    public void LowBattery_OnlyBelowFivePercent(double? battery, bool expectedLow)
    {
        var rooms = ClimateRoom.From(new TemperatureStatus { Rooms = [room("Boys", 70, battery)] }, DateTime.UtcNow);

        Assert.Equal(expectedLow, Assert.Single(rooms).LowBattery);
    }

    [Fact]
    public void AnySensorLow_SeesHiddenAndOutsideSensors()
    {
        Assert.True(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Boys", null, 2)] }));
        Assert.True(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Outside", 50, 1)] }));
        Assert.False(ClimateRoom.AnySensorLow(new TemperatureStatus { Rooms = [room("Boys", 70, 50)] }));
        Assert.False(ClimateRoom.AnySensorLow(null));
    }

    [Fact]
    public void Rooms_NullStatus_IsEmpty() => Assert.Empty(ClimateRoom.From(null, DateTime.UtcNow));

    private static readonly DateTime now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static RoomReading heard(string name, double? temperature, int minutesAgo) =>
        new() { Name = name, Temperature = temperature, LastReadingAt = now.AddMinutes(-minutesAgo) };

    private static TemperatureStatus withAges(params RoomReading[] rooms) =>
        new() { OutsideRoomName = "Outside", ShowsReadingAge = true, Rooms = [.. rooms] };

    [Fact]
    public void WithReadingAges_EveryIndoorRoomIsListed_WithItsAge()
    {
        var rooms = ClimateRoom.From(
            withAges(
                heard("Outside", 50, 1),
                heard("Fresh", 70, 2),
                heard("Aging", 68, 7),
                new RoomReading { Name = "Never" },
                new RoomReading { Name = "Lost", LastReadingAt = now.AddMinutes(-34) }),
            now);

        Assert.Equal(["Fresh", "Aging", "Never", "Lost"], rooms.Select(r => r.Label));
        Assert.Equal(new double?[] { 70, 68, null, null }, rooms.Select(r => r.Temperature));
        Assert.Equal([null, "7 min old", "No reading yet", "No reading for 34 min"], rooms.Select(r => r.Note));
    }

    [Fact]
    public void WithReadingAges_ValueThatAgedPastTwentyMinutesSinceFetch_IsHidden()
    {
        var room = Assert.Single(ClimateRoom.From(withAges(heard("Kitchen", 70, 21)), now));

        Assert.Null(room.Temperature);
        Assert.Equal("No reading for 21 min", room.Note);
    }

    [Fact]
    public void Outside_WithReadingAges_CarriesNote()
    {
        var outside = ClimateRoom.Outside(withAges(new RoomReading { Name = "Outside" }), now);

        Assert.NotNull(outside);
        Assert.Null(outside.Temperature);
        Assert.Equal("No reading yet", outside.Note);
    }

    [Fact]
    public void Outside_WithoutReadingAges_HasNoNote()
    {
        var outside = ClimateRoom.Outside(new TemperatureStatus { OutsideRoomName = "Outside", OutsideTemperature = 50, Rooms = [room("Outside", 50, 2)] }, now);

        Assert.NotNull(outside);
        Assert.Equal(50, outside.Temperature);
        Assert.Null(outside.Note);
        Assert.True(outside.LowBattery);
    }

    private static TemperatureStatus advice(double outside, double girls, bool shouldClose) => new()
    {
        OutsideTemperature = outside,
        IndoorRoomName = "Girls",
        IndoorTemperature = girls,
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
        Assert.Null(WindowAdvice.From(new TemperatureStatus { IndoorTemperature = 70 }));
    }

    [Fact]
    public void WindowAdvice_Celsius_ConvertsDifference()
    {
        var result = WindowAdvice.From(advice(60, 69, shouldClose: false), TemperatureUnit.Celsius);

        Assert.Equal("Open windows · 5° cooler outside", result!.Text);
    }
}
