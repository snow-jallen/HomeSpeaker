using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class NowPlayingInfoTests
{
    private static PlayerStatus song(string? artist, string? album) => new()
    {
        StillPlaying = true,
        PercentComplete = 0.38m,
        Elapsed = TimeSpan.FromSeconds(114),
        Remaining = TimeSpan.FromSeconds(188),
        CurrentSong = new Song { SongId = 42, Name = "Clair de Lune", Artist = artist, Album = album },
    };

    [Fact]
    public void From_Null_ReturnsNull() => Assert.Null(NowPlayingInfo.From(null));

    [Fact]
    public void From_NotPlaying_ReturnsNull() => Assert.Null(NowPlayingInfo.From(song("Debussy", null) with { StillPlaying = false }));

    [Fact]
    public void From_Song_JoinsArtistAndAlbum()
    {
        var info = NowPlayingInfo.From(song("Claude Debussy", "Suite bergamasque"))!;

        Assert.Equal("Clair de Lune", info.Title);
        Assert.Equal("Claude Debussy · Suite bergamasque", info.Subtitle);
        Assert.Equal(42, info.SongId);
        Assert.False(info.IsStream);
        Assert.Equal(0.38, info.Percent, 3);
    }

    [Fact]
    public void From_Song_DropsPlaceholderArtistAndAlbum() =>
        Assert.Null(NowPlayingInfo.From(song("[ No Artist ]", "[ No Album ]"))!.Subtitle);

    [Fact]
    public void From_StreamWithIcyTitle_ShowsTrackOverStation()
    {
        var info = NowPlayingInfo.From(new PlayerStatus
        {
            StillPlaying = true,
            IsStream = true,
            StreamName = "Classical KUSC",
            CurrentSong = new Song { Name = "Gymnopédie No. 1" },
        })!;

        Assert.Equal("Gymnopédie No. 1", info.Title);
        Assert.Equal("Classical KUSC", info.Subtitle);
        Assert.Null(info.SongId);
        Assert.True(info.IsStream);
        Assert.Equal("Classical KUSC", info.Station);
    }

    [Fact]
    public void From_StreamWithoutIcy_ShowsStationAsLive()
    {
        var info = NowPlayingInfo.From(new PlayerStatus { StillPlaying = true, IsStream = true, StreamName = "Classical KUSC" })!;

        Assert.Equal("Classical KUSC", info.Title);
        Assert.Equal("Live radio", info.Subtitle);
        Assert.Equal("Classical KUSC", info.Station);
    }

    [Fact]
    public void From_Song_HasNoStation() => Assert.Null(NowPlayingInfo.From(song("Debussy", null))!.Station);
}
