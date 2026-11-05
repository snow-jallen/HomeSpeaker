using System.Net;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared.Anchors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Anchors;

/// <summary>This device's anchors with an external feed (e.g. Metanoeo) merged in.</summary>
public sealed class LocalAnchorStoreFeedTests : IAsyncDisposable
{
    private static readonly DateTimeOffset noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly today = new(2026, 11, 4);

    private const string payload = """
        {
          "people": [ { "id": "u1", "name": "Dad" }, { "id": "u2", "name": "Emma" } ],
          "anchors": [
            { "id": "h1", "personId": "u1", "title": "Stretch", "slot": "morning", "order": 0, "activeFrom": "2026-11-01", "writable": true },
            { "id": "h2", "personId": "u1", "title": "Lights out", "slot": "evening", "order": 1, "targetTime": "22:30",
              "activeFrom": "2026-11-01", "missingIs": "unknown", "writable": false },
            { "id": "h3", "personId": "u2", "title": "Piano", "slot": "day", "order": 0, "activeFrom": "2026-11-01", "writable": true }
          ],
          "logs": []
        }
        """;

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly FakeFeedServer server = new();
    private readonly AnchorChangeNotifier notifier = new();
    private readonly AnchorFeedSignal signal = new();
    private MusicContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    private async Task<LocalAnchorStore> storeAsync()
    {
        await connection.OpenAsync();
        db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var time = new FixedTimeProvider(noon, TimeZoneInfo.Utc);
        var feed = new HttpAnchorFeed(
            server,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anchors:Feed:Name"] = "Metanoeo",
                ["Anchors:Feed:Url"] = "https://feed.example/",
            }).Build(),
            time,
            NullLogger<HttpAnchorFeed>.Instance);
        server.Respond(HttpStatusCode.OK, payload);
        return new LocalAnchorStore(db, time, notifier, feed, signal);
    }

    private static async Task<string> dadAsync(LocalAnchorStore store, string? link = "u1")
    {
        var dad = (await store.CreatePersonAsync(new AnchorPersonRequest("Dad"))).Value!;
        if (link is not null)
        {
            Assert.Equal(AnchorResult.Ok, await store.UpdatePersonAsync(dad.Id, new AnchorPersonRequest("Dad", FeedPersonId: link)));
        }

        return dad.Id;
    }

    [Fact]
    public async Task Board_LinkedPersonGetsFeedAnchors_UnlinkedFeedPersonAppears()
    {
        var store = await storeAsync();
        var dad = await dadAsync(store);

        var board = await store.GetBoardAsync();

        Assert.Equal(["Dad", "Emma"], board.People.Select(p => p.Name));
        Assert.Equal(["Stretch", "Lights out"], board.People[0].Anchors.Select(a => a.Title));
        Assert.Equal((dad, "u1"), (board.People[0].Id, board.People[0].FeedPersonId));
        Assert.Equal(("feed:u2", "Metanoeo"), (board.People[1].Id, board.People[1].Source));
        Assert.Equal(("Metanoeo", "ok"), (board.Feed!.Name, board.Feed.Status));
        Assert.Equal(["u1", "u2"], board.Feed.People!.Select(p => p.Id));
    }

    [Fact]
    public async Task Board_FeedUnreachable_StillShowsLocal()
    {
        var store = await storeAsync();
        await dadAsync(store);
        server.Fail();

        var board = await store.GetBoardAsync();

        Assert.Equal("unreachable", board.Feed!.Status);
        Assert.Empty(Assert.Single(board.People).Anchors);
    }

    [Fact]
    public async Task SetDone_FeedAnchor_QueuesAndShowsPending()
    {
        var store = await storeAsync();
        await dadAsync(store);
        var woke = 0;
        signal.Woken += () => woke++;

        Assert.Equal(AnchorResult.Ok, await store.SetDoneAsync("feed:h1", today, new AnchorLogRequest(true)));

        var cell = (await store.GetBoardAsync()).People[0].Anchors.Single(a => a.Id == "feed:h1").Cells.Single();
        Assert.Equal(("kept", true), (cell.State, cell.Pending));
        Assert.Equal(1, woke);
    }

    [Fact]
    public async Task SetDone_ReadOnlyFeedAnchor_ReadOnly_UnknownFeedAnchor_NotFound()
    {
        var store = await storeAsync();

        Assert.Equal(AnchorResult.ReadOnly, await store.SetDoneAsync("feed:h2", today, new AnchorLogRequest(true)));
        Assert.Equal(AnchorResult.NotFound, await store.SetDoneAsync("feed:nope", today, new AnchorLogRequest(true)));
    }

    [Fact]
    public async Task EditingFeedAnchor_ReadOnly()
    {
        var store = await storeAsync();

        Assert.Equal(AnchorResult.ReadOnly, await store.UpdateAnchorAsync("feed:h1", new AnchorRequest("Renamed")));
        Assert.Equal(AnchorResult.ReadOnly, await store.RetireAnchorAsync("feed:h1"));
        Assert.Equal(AnchorResult.ReadOnly, (await store.CreateAnchorAsync("feed:u2", new AnchorRequest("New"))).Result);
    }

    [Fact]
    public async Task Link_IsOneToOne_AndEmptyClears()
    {
        var store = await storeAsync();
        var dad = await dadAsync(store);
        var other = (await store.CreatePersonAsync(new AnchorPersonRequest("Also Dad"))).Value!;

        await store.UpdatePersonAsync(other.Id, new AnchorPersonRequest("Also Dad", FeedPersonId: "u1"));
        var board = await store.GetBoardAsync();
        Assert.Null(board.People.Single(p => p.Id == dad).FeedPersonId);
        Assert.Equal("u1", board.People.Single(p => p.Id == other.Id).FeedPersonId);

        await store.UpdatePersonAsync(other.Id, new AnchorPersonRequest("Also Dad", FeedPersonId: string.Empty));
        Assert.Null((await store.GetBoardAsync()).People.Single(p => p.Id == other.Id).FeedPersonId);
    }

    [Fact]
    public async Task GetDay_FeedPerson()
    {
        var store = await storeAsync();

        var emma = await store.GetDayAsync("feed:u2");

        Assert.Equal("Piano", Assert.Single(emma!.Anchors).Title);
    }
}
