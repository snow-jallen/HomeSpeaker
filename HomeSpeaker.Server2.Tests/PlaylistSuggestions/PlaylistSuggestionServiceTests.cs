using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.PlaylistSuggestions;
using HomeSpeaker.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.PlaylistSuggestions;

public sealed class PlaylistSuggestionServiceTests : IAsyncDisposable
{
    private static readonly string[] SongPaths = ["/music/a.mp3", "/music/b.mp3", "/music/c.mp3"];

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Mp3Library library = new(new FakeFileSource(), new FakeTagParser(), new OnDiskDataStore(), NullLogger<Mp3Library>.Instance);
    private MusicContext? db;

    [Fact]
    public async Task ApprovingAddsToThePlaylistInReviewOrderAndRejectingTakesItBackOut()
    {
        var service = await createAsync();
        var songs = await service.GetSongsAsync("Quiet");

        await service.DecideAsync(songs[1].SuggestionId, SuggestionStatus.Approved);
        await service.DecideAsync(songs[0].SuggestionId, SuggestionStatus.Approved);
        Assert.Equal(["/music/b.mp3", "/music/a.mp3"], await playlistAsync("Quiet"));

        await service.DecideAsync(songs[1].SuggestionId, SuggestionStatus.Rejected);
        Assert.Equal(["/music/a.mp3"], await playlistAsync("Quiet"));

        var summary = Assert.Single(await service.GetPlaylistsAsync(), s => s.Name == "Quiet");
        Assert.Equal((1, 1, 1), (summary.Pending, summary.Approved, summary.Rejected));
    }

    [Fact]
    public async Task ApprovingASongAlreadyInThePlaylistDoesNotAddItTwice()
    {
        var service = await createAsync();
        db!.Playlists.Add(new Data.Playlist { Name = "Quiet", Songs = [new() { SongPath = "/music/a.mp3" }] });
        await db.SaveChangesAsync();
        var songs = await service.GetSongsAsync("Quiet");

        await service.DecideAsync(songs[0].SuggestionId, SuggestionStatus.Approved);

        Assert.Equal(["/music/a.mp3"], await playlistAsync("Quiet"));
    }

    [Fact]
    public async Task ApproveRemainingLeavesRejectedSongsOut()
    {
        var service = await createAsync();
        var songs = await service.GetSongsAsync("Quiet");
        await service.DecideAsync(songs[2].SuggestionId, SuggestionStatus.Rejected);

        var approved = await service.ApproveRemainingAsync("Quiet");

        Assert.Equal(2, approved);
        Assert.Equal(["/music/a.mp3", "/music/b.mp3"], await playlistAsync("Quiet"));
    }

    [Fact]
    public async Task SongsNoLongerInTheLibraryAreLeftOut()
    {
        var service = await createAsync();

        var songs = await service.GetSongsAsync("Fun");

        Assert.Equal(["/music/c.mp3"], songs.Select(s => s.Path));
    }

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }

        await connection.DisposeAsync();
        library.Dispose();
    }

    private async Task<PlaylistSuggestionService> createAsync()
    {
        await connection.OpenAsync();
        db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.PlaylistSuggestions.AddRange(
            new PlaylistSuggestion { PlaylistName = "Quiet", SongPath = "/music/a.mp3", SortOrder = 0 },
            new PlaylistSuggestion { PlaylistName = "Quiet", SongPath = "/music/b.mp3", SortOrder = 1 },
            new PlaylistSuggestion { PlaylistName = "Quiet", SongPath = "/music/c.mp3", SortOrder = 2 },
            new PlaylistSuggestion { PlaylistName = "Fun", SongPath = "/music/missing.mp3", SortOrder = 0 },
            new PlaylistSuggestion { PlaylistName = "Fun", SongPath = "/music/c.mp3", SortOrder = 1 });
        await db.SaveChangesAsync();
        var playlists = new PlaylistService(db, library, NullLogger<PlaylistService>.Instance, new RecordingMusicPlayer(), new HomeSpeaker.Server2.Services.PlayingPlaylist.PlayingPlaylistTracker());
        return new PlaylistSuggestionService(db, library, playlists, TimeProvider.System);
    }

    private async Task<List<string>> playlistAsync(string name) =>
        (await db!.Playlists.AsNoTracking().Include(p => p.Songs).SingleAsync(p => p.Name == name))
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
