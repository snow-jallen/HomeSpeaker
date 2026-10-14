using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class StationKeyTests
{
    private static PlayerStatus stream(string? path, string? name) => new()
    {
        IsStream = true,
        StreamName = name,
        CurrentSong = new Song { Name = "Now Playing Title", Path = path ?? string.Empty },
    };

    [Fact]
    public void ANonStreamHasNoKey()
    {
        Assert.Null(DashboardState.StationKey(null));
        Assert.Null(DashboardState.StationKey(new PlayerStatus { IsStream = false, StreamName = "KBAQ" }));
    }

    [Fact]
    public void TheSameStationHasTheSameKeyWhateverTheIcyTitle()
    {
        var first = stream("http://example.com/live", "KBAQ");
        var second = first with { CurrentSong = first.CurrentSong! with { Name = "A different track" } };

        Assert.Equal(DashboardState.StationKey(first), DashboardState.StationKey(second));
    }

    [Theory]
    [InlineData("http://example.com/b", "KBAQ")] // another URL
    [InlineData("http://example.com/a", "KBAQ HD2")] // another name
    public void AnotherStationHasAnotherKey(string path, string name) =>
        Assert.NotEqual(DashboardState.StationKey(stream("http://example.com/a", "KBAQ")), DashboardState.StationKey(stream(path, name)));
}
