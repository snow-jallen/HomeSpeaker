using System.Net;
using System.Text.Json;
using HomeSpeaker.Server2.Services.PlayingPlaylist;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.PlayingPlaylist;

public sealed class PlayingPlaylistEndpointTests : IDisposable
{
    private readonly RecordingMusicPlayer player = new();
    private readonly TestServerFactory factory = new();
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app;

    public PlayingPlaylistEndpointTests()
    {
        app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IMusicPlayer>(player)));
    }

    public void Dispose()
    {
        app.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task Status_NamesThePlaylistThePlayingSongCameFrom()
    {
        playFromPlaylist("Sunday");

        var json = await app.CreateClient().GetStringAsync("/api/homespeaker/player/status");

        Assert.Equal("Sunday", JsonDocument.Parse(json).RootElement.GetProperty("playingPlaylist").GetString());
    }

    [Fact]
    public async Task Status_HasNoPlaylist_WhenNoneIsPlaying()
    {
        player.PlaySong(new Song { SongId = 1, Name = "Loose", Path = "/music/loose.mp3" });

        var json = await app.CreateClient().GetStringAsync("/api/homespeaker/player/status");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.True(!root.TryGetProperty("playingPlaylist", out var name) || name.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task RemoveFromPlaylist_WhenNoPlaylistIsPlaying_Returns409()
    {
        player.PlaySong(new Song { SongId = 1, Name = "Loose", Path = "/music/loose.mp3" });

        var response = await app.CreateClient().PostAsync("/api/homespeaker/player/remove-from-playlist", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, player.SkipCount);
    }

    [Fact]
    public async Task RemoveFromPlaylist_WhilePlayingOne_SkipsTheSong()
    {
        playFromPlaylist("Sunday");

        var response = await app.CreateClient().PostAsync("/api/homespeaker/player/remove-from-playlist", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, player.SkipCount);
    }

    private void playFromPlaylist(string name)
    {
        player.PlaySong(new Song { SongId = 1, Name = "Hymn", Path = "/music/hymn.mp3" });
        app.Services.GetRequiredService<PlayingPlaylistTracker>().Started(name, ["/music/hymn.mp3"]);
    }
}
