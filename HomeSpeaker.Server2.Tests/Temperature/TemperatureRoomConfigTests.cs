using HomeSpeaker.Server2.Services;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Temperature;

public sealed class TemperatureRoomConfigTests
{
    public const string OwnerRooms = "Outside: Outside; Greenhouse: Greenhouse; Emma: Emma; Boys: Boy; Downstairs: Downstairs, Mom, Dad, Parent; Girls: Girl";

    private static IConfiguration build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Parse_OwnerString_KeepsOrderAndPatterns()
    {
        var rooms = TemperatureRoomConfig.Parse(OwnerRooms);

        Assert.Equal(["Outside", "Greenhouse", "Emma", "Boys", "Downstairs", "Girls"], rooms.Select(r => r.Room));
        Assert.Equal(["Downstairs", "Mom", "Dad", "Parent"], rooms.Single(r => r.Room == "Downstairs").Patterns);
        Assert.Equal(["Boy"], rooms.Single(r => r.Room == "Boys").Patterns);
    }

    [Fact]
    public void Parse_EntryWithoutColon_UsesNameAsPattern()
    {
        var room = Assert.Single(TemperatureRoomConfig.Parse("Garage"));

        Assert.Equal("Garage", room.Room);
        Assert.Equal(["Garage"], room.Patterns);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsEmpty(string? value) => Assert.Empty(TemperatureRoomConfig.Parse(value));

    [Fact]
    public void Parse_TrimsAndSkipsEmptyEntries()
    {
        var rooms = TemperatureRoomConfig.Parse(" A: x ;; B ");

        Assert.Equal(["A", "B"], rooms.Select(r => r.Room));
        Assert.Equal(["x"], rooms[0].Patterns);
        Assert.Equal(["B"], rooms[1].Patterns);
    }

    [Fact]
    public void FromConfiguration_IndoorDefaultsToFirstNonOutsideRoom()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:Rooms"] = OwnerRooms }));

        Assert.Equal("Outside", settings.OutsideRoom);
        Assert.Equal("Greenhouse", settings.IndoorRoom);
    }

    [Fact]
    public void FromConfiguration_ExplicitOutsideAndIndoor_AreUsed()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new()
        {
            ["Temperature:Rooms"] = OwnerRooms,
            ["Temperature:OutsideRoom"] = "Greenhouse",
            ["Temperature:IndoorRoom"] = "Girls",
        }));

        Assert.Equal("Greenhouse", settings.OutsideRoom);
        Assert.Equal("Girls", settings.IndoorRoom);
    }

    [Fact]
    public void FromConfiguration_NoOutsideRoom_WindowAdviceOff()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:Rooms"] = "Kitchen: Kit; Den: Den" }));

        Assert.Null(settings.OutsideRoom);
        Assert.False(settings.WindowAdvice);
    }

    [Fact]
    public void FromConfiguration_WindowAdviceFalse_TurnsItOff()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:Rooms"] = OwnerRooms, ["Temperature:WindowAdvice"] = "false" }));

        Assert.False(settings.WindowAdvice);
    }

    [Fact]
    public void FromConfiguration_WindowMinOutsideDefaultsTo50_AndThresholdTo2()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:Rooms"] = OwnerRooms }));

        Assert.True(settings.WindowAdvice);
        Assert.Equal(50, settings.WindowMinOutsideF);
        Assert.Equal(2, settings.ThresholdF);
    }

    [Fact]
    public void FromConfiguration_WindowMinOutside_IsReadFromConfig()
    {
        var settings = TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:WindowMinOutside"] = "40", ["TemperatureThreshold"] = "3" }));

        Assert.Equal(40, settings.WindowMinOutsideF);
        Assert.Equal(3, settings.ThresholdF);
    }

    [Fact]
    public void FromConfiguration_EmptyRooms_OutsideOnlyWhenNamedExplicitly()
    {
        Assert.Null(TemperatureRoomConfig.FromConfiguration(build([])).OutsideRoom);
        Assert.Equal("Patio", TemperatureRoomConfig.FromConfiguration(build(new() { ["Temperature:OutsideRoom"] = "Patio" })).OutsideRoom);
    }
}
