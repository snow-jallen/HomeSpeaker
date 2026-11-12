using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.PlayHistory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.PlayHistory;

public sealed class SmartPlaylistRulesTests
{
    private static readonly DateTime now = new(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> library = ["/music/a.mp3", "/music/b.mp3", "/music/c.mp3", "/music/old.mp3"];

    [Fact]
    public void MostLovedNeedsThreePlaysAndFewSkipsOrderedByPlays()
    {
        var history = plays("/music/a.mp3", 3, daysAgo: 1)
            .Concat(plays("/music/b.mp3", 5, daysAgo: 1))
            .Concat(plays("/music/c.mp3", 3, daysAgo: 1)).Concat(skips("/music/c.mp3", 2)) // 40% skipped
            .Concat(plays("/music/gone.mp3", 9, daysAgo: 1)); // not in the library

        var loved = build(history).Single(p => p.Kind == SmartPlaylistRules.MostLovedKind);

        Assert.Equal(["/music/b.mp3", "/music/a.mp3"], loved.SongPaths);
    }

    [Fact]
    public void RediscoverIsLikedSongsNotHeardForMonths()
    {
        var history = plays("/music/old.mp3", 2, daysAgo: 200)
            .Concat(plays("/music/a.mp3", 2, daysAgo: 200)).Concat(plays("/music/a.mp3", 1, daysAgo: 3))
            .Concat(plays("/music/b.mp3", 1, daysAgo: 300));

        var rediscover = build(history).Single(p => p.Kind == SmartPlaylistRules.RediscoverKind);

        Assert.Equal(["/music/old.mp3"], rediscover.SongPaths);
    }

    [Fact]
    public async Task ApplyCreatesOnlyOnceThereAreEnoughSongsAndThenReplacesThem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var few = new SmartPlaylist(SmartPlaylistRules.MostLovedKind, SmartPlaylistRules.MostLovedName, ["/music/a.mp3"]);
        await SmartPlaylistWorker.ApplyAsync(db, [few], NullLogger.Instance, CancellationToken.None);
        Assert.Empty(db.Playlists);

        var many = few with { SongPaths = Enumerable.Range(0, 12).Select(i => $"/music/{i}.mp3").ToList() };
        await SmartPlaylistWorker.ApplyAsync(db, [many], NullLogger.Instance, CancellationToken.None);
        await SmartPlaylistWorker.ApplyAsync(db, [few], NullLogger.Instance, CancellationToken.None);

        var playlist = await db.Playlists.Include(p => p.Songs).SingleAsync();
        Assert.Equal(SmartPlaylistRules.MostLovedKind, playlist.AutoKind);
        Assert.Equal(["/music/a.mp3"], playlist.Songs.Select(s => s.SongPath));
    }

    [Fact]
    public async Task ApplyLeavesAPlaylistSomeoneMadeWithTheSameNameAlone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Playlists.Add(new Data.Playlist { Name = SmartPlaylistRules.MostLovedName, Songs = [new() { SongPath = "/music/mine.mp3" }] });
        await db.SaveChangesAsync();

        var smart = new SmartPlaylist(SmartPlaylistRules.MostLovedKind, SmartPlaylistRules.MostLovedName,
            Enumerable.Range(0, 12).Select(i => $"/music/{i}.mp3").ToList());
        await SmartPlaylistWorker.ApplyAsync(db, [smart], NullLogger.Instance, CancellationToken.None);

        var playlist = await db.Playlists.Include(p => p.Songs).SingleAsync();
        Assert.Null(playlist.AutoKind);
        Assert.Equal(["/music/mine.mp3"], playlist.Songs.Select(s => s.SongPath));
    }

    private static IReadOnlyList<SmartPlaylist> build(IEnumerable<PlayRecord> history) => SmartPlaylistRules.Build(history, library, now);

    private static IEnumerable<PlayRecord> plays(string path, int count, int daysAgo) =>
        Enumerable.Range(0, count).Select(i => new PlayRecord(path, now.AddDays(-daysAgo).AddHours(-i), Skipped: false));

    private static IEnumerable<PlayRecord> skips(string path, int count) =>
        Enumerable.Range(0, count).Select(i => new PlayRecord(path, now.AddDays(-1).AddHours(-i), Skipped: true));
}
