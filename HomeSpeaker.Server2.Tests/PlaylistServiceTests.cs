using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
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
        return new PlaylistService(dbContext, library, NullLogger<PlaylistService>.Instance, player);
    }

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
