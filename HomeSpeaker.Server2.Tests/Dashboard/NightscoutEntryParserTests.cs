using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class NightscoutEntryParserTests
{
    [Theory]
    [InlineData("2026-10-13T12:00:00.000Z")]
    [InlineData("2026-10-13T06:00:00.000-06:00")]
    [InlineData("2026-10-13T06:00:00.000-0600")]
    [InlineData("2026-10-13T14:00:00+0200")]
    [InlineData("2026-10-13T12:00:00")]
    public void ParseDateString_HandlesTheShapesUploadersWrite(string value) =>
        Assert.Equal(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), NightscoutEntryParser.ParseDateString(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseDateString_GarbageIsNull(string? value) => Assert.Null(NightscoutEntryParser.ParseDateString(value));

    [Fact]
    public void Parse_UsesEpochMsOverDateString()
    {
        var epoch = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var readings = NightscoutEntryParser.Parse($"[{{\"sgv\":120,\"date\":{epoch},\"dateString\":\"1999-01-01T00:00:00Z\",\"direction\":\"Flat\",\"type\":\"sgv\"}}]");

        var reading = Assert.Single(readings);
        Assert.Equal(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), reading.Date);
        Assert.Equal("Flat", reading.Direction);
    }

    [Fact]
    public void Parse_SkipsOnlyTheBadEntries()
    {
        var readings = NightscoutEntryParser.Parse("""
            [
              {"sgv":100,"dateString":"2026-10-13T12:00:00Z"},
              {"sgv":"x","dateString":"2026-10-13T12:05:00Z"},
              {"dateString":"2026-10-13T12:10:00Z"},
              {"sgv":110,"dateString":"garbage"},
              "stray",
              {"sgv":130,"date":1760356800000}
            ]
            """);

        Assert.Equal([100, 130], readings.Select(r => r.Sgv));
    }

    [Fact]
    public void Parse_NotAnArray_IsEmpty() => Assert.Empty(NightscoutEntryParser.Parse("{}"));
}
