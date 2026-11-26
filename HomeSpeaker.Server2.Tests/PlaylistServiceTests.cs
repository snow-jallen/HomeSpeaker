using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.PlayingPlaylist;
using HomeSpeaker.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests;

public sealed class PlaylistServiceTests : IAsyncDisposable
{
    private static readonly string[] SongPaths = ["/music/a.mp3", "/music/b.mp3", "/music/c.mp3", "/music/other.mp3"];

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly RecordingMusicPlayer player = new();
    private readonly PlayingPlaylistTracker tracker = new();
    private readonly Mp3Library library = new(new FakeFileSource(), new FakeTagParser(), new OnDiskDataStore(), NullLogger<Mp3Library>.Instance);
    private MusicContext? dbContext;

    [Fact]
    public async Task PlayPlaylistReplacesWhatIsPlayingAndQueued()
    {
        var service = await createServiceAsync();
        var other = library.Songs.Single(s => s.Path == "/music/other.mp3");
        player.PlaySong(other);
        player.EnqueueSong(other);

        await service.PlayPlaylistAsync("Mix");

        Assert.Equal(1, player.StopCount);
        Assert.Equal("/music/a.mp3", player.Played[^1].Path);
        Assert.Equal(["/music/b.mp3", "/music/c.mp3"], player.Enqueued.Select(s => s.Path));
    }

    [Fact]
    public async Task PlayPlaylistWithNoPlayableSongsLeavesPlaybackAlone()
    {
        var service = await createServiceAsync();
        dbContext!.Playlists.Add(new Data.Playlist { Name = "Gone", Songs = [new() { SongPath = "/music/missing.mp3", Order = 0 }] });
        await dbContext.SaveChangesAsync();
        var other = library.Songs.Single(s => s.Path == "/music/other.mp3");
        player.PlaySong(other);
        player.EnqueueSong(other);

        await service.PlayPlaylistAsync("Gone");

        Assert.Equal(0, player.StopCount);
        Assert.Single(player.Enqueued);
    }

    [Fact]
    public async Task PlayPlaylistRemembersWhichPlaylistIsPlaying()
    {
        var service = await createServiceAsync();

        await service.PlayPlaylistAsync("Mix");

        Assert.Equal("Mix", tracker.PlaylistFor(player.Status.CurrentSong));
    }

    [Fact]
    public async Task RemovePlayingSongTakesItOutOfThePlaylistAndSkipsIt()
    {
        var service = await createServiceAsync();
        await service.PlayPlaylistAsync("Mix");

        var removed = await service.RemovePlayingSongAsync();

        Assert.True(removed);
        Assert.Equal(["/music/b.mp3", "/music/c.mp3"], await playlistPathsAsync("Mix"));
        Assert.Equal(1, player.SkipCount);
        Assert.Null(tracker.PlaylistFor(library.Songs.Single(s => s.Path == "/music/a.mp3")));
    }

    [Fact]
    public async Task RemovePlayingSongDoesNothingWhenNoPlaylistIsPlaying()
    {
        var service = await createServiceAsync();
        player.PlaySong(library.Songs.Single(s => s.Path == "/music/a.mp3"));

        var removed = await service.RemovePlayingSongAsync();

        Assert.False(removed);
        Assert.Equal(3, (await playlistPathsAsync("Mix")).Count);
        Assert.Equal(0, player.SkipCount);
    }

    [Fact]
    public async Task RemovePlayingSongRejectsItsApprovedSuggestionSoItIsNotSuggestedAgain()
    {
        var service = await createServiceAsync();
        dbContext!.PlaylistSuggestions.Add(new PlaylistSuggestion { PlaylistName = "Mix", SongPath = "/music/a.mp3", Status = SuggestionStatus.Approved });
        await dbContext.SaveChangesAsync();
        await service.PlayPlaylistAsync("Mix");

        await service.RemovePlayingSongAsync();

        Assert.Equal(SuggestionStatus.Rejected, (await dbContext.PlaylistSuggestions.AsNoTracking().SingleAsync()).Status);
    }

    public async ValueTask DisposeAsync()
    {
        if (dbContext is not null)
        {
            await dbContext.DisposeAsync();
        }

        await connection.DisposeAsync();
        library.Dispose();
    }

    private async Task<PlaylistService> createServiceAsync()
    {
        await connection.OpenAsync();
        dbContext = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Playlists.Add(new Data.Playlist
        {
            Name = "Mix",
            Songs =
            [
                new() { SongPath = "/music/c.mp3", Order = 2 },
                new() { SongPath = "/music/a.mp3", Order = 0 },
                new() { SongPath = "/music/b.mp3", Order = 1 },
            ],
        });
        await dbContext.SaveChangesAsync();
        return new PlaylistService(dbContext, library, NullLogger<PlaylistService>.Instance, player, tracker);
    }

    private async Task<List<string>> playlistPathsAsync(string name) =>
        (await dbContext!.Playlists.AsNoTracking().Include(p => p.Songs).SingleAsync(p => p.Name == name))
            .Songs.OrderBy(s => s.Order).Select(s => s.SongPath).ToList();

    private sealed class FakeFileSource : IFileSource
    {
        // A folder that doesn't exist, so the library skips its filesystem watcher.
        public string RootFolder => Path.Combine(Path.GetTempPath(), "homespeaker-missing-" + Guid.NewGuid().ToString("N"));

        public IEnumerable<string> GetAllMp3s() => SongPaths;

        public void SoftDelete(string path) => throw new NotSupportedException();
    }

    private sealed class FakeTagParser : ITagParser
    {
        public Song CreateSong(string fullPath) => new() { Name = Path.GetFileNameWithoutExtension(fullPath), Path = fullPath };

        public void UpdateSongTags(string fullPath, string name, string artist, string album) => throw new NotSupportedException();
    }
}
