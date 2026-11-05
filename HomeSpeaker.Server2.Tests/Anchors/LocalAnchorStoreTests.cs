using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared.Anchors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class LocalAnchorStoreTests : IAsyncDisposable
{
    // Noon UTC on a Wednesday; the provider's local zone is UTC unless a test says otherwise.
    private static readonly DateTimeOffset noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly today = new(2026, 11, 4);

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly AnchorChangeNotifier notifier = new();
    private MusicContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    private async Task<LocalAnchorStore> storeAsync(DateTimeOffset? now = null, TimeZoneInfo? zone = null)
    {
        if (db is null)
        {
            await connection.OpenAsync();
            db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
        }

        return new LocalAnchorStore(db, new FixedTimeProvider(now ?? noon, zone ?? TimeZoneInfo.Utc), notifier);
    }

    private static async Task<string> personAsync(LocalAnchorStore store, string name)
    {
        var outcome = await store.CreatePersonAsync(new AnchorPersonRequest(name));
        Assert.True(outcome.Succeeded, outcome.Message);
        return outcome.Value!.Id;
    }

    private static async Task<string> anchorAsync(LocalAnchorStore store, string personId, string title, string slot = "day", int? days = null)
    {
        var outcome = await store.CreateAnchorAsync(personId, new AnchorRequest(title, slot, Days: days));
        Assert.True(outcome.Succeeded, outcome.Message);
        return outcome.Value!.Id;
    }

    /// <summary>Moves an anchor's start back, as if it had been created on <paramref name="from"/>.</summary>
    private async Task backdateAsync(string anchorId, DateOnly from)
    {
        var period = await db!.AnchorPeriods.SingleAsync(p => p.AnchorId == int.Parse(anchorId, System.Globalization.CultureInfo.InvariantCulture));
        period.From = from;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Board_PeopleOrderedAndAnchorsBySlot()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        await personAsync(store, "Emma");
        await anchorAsync(store, sam, "Lights out", "evening");
        await anchorAsync(store, sam, "Up by 6", "morning");
        await anchorAsync(store, sam, "Walk");

        var board = await store.GetBoardAsync();

        Assert.Equal(["Sam", "Emma"], board.People.Select(p => p.Name));
        Assert.Equal(["Up by 6", "Walk", "Lights out"], AnchorView.Ordered(board.People[0].Anchors).Select(a => a.Title));
        Assert.All(board.People[0].Anchors, a => Assert.Equal("local", a.Source));
    }

    [Fact]
    public async Task Board_TodayUsesLocalZone()
    {
        // 03:00 UTC on the 5th is still the evening of the 4th in Denver.
        var store = await storeAsync(new DateTimeOffset(2026, 11, 5, 3, 0, 0, TimeSpan.Zero), TimeZoneInfo.FindSystemTimeZoneById("America/Denver"));

        Assert.Equal(today, (await store.GetBoardAsync()).Today);
    }

    [Fact]
    public async Task Board_CellsCoverRange()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");
        await backdateAsync(walk, today.AddDays(-2));
        await store.SetDoneAsync(walk, today.AddDays(-1), new AnchorLogRequest(true));

        var board = await store.GetBoardAsync(today.AddDays(-3), today);

        var cells = board.People[0].Anchors[0].Cells;
        Assert.Equal([today.AddDays(-3), today.AddDays(-2), today.AddDays(-1), today], cells.Select(c => c.Date));
        Assert.Equal(["none", "missed", "kept", "open"], cells.Select(c => c.State));
    }

    [Fact]
    public async Task Board_RangeOver92Days_Clamped()
    {
        var store = await storeAsync();

        var board = await store.GetBoardAsync(today.AddDays(-200), today);

        Assert.Equal(today.AddDays(-91), board.From);
    }

    [Fact]
    public async Task Board_TodayCountsAndStreaks()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");
        var read = await anchorAsync(store, sam, "Read");
        await backdateAsync(walk, today.AddDays(-5));
        await backdateAsync(read, today.AddDays(-5));
        foreach (var offset in new[] { 0, 1, 2 })
        {
            await store.SetDoneAsync(walk, today.AddDays(-offset), new AnchorLogRequest(true));
        }

        await store.SetDoneAsync(read, today.AddDays(-1), new AnchorLogRequest(true));
        await store.SetDoneAsync(read, today.AddDays(-2), new AnchorLogRequest(true));

        var person = (await store.GetBoardAsync()).People[0];

        Assert.Equal((1, 2), (person.TodayDone, person.TodayTotal));
        Assert.Equal(3, person.Anchors.Single(a => a.Title == "Walk").Streak);
        Assert.Equal(2, person.Anchors.Single(a => a.Title == "Read").Streak);
        Assert.Equal(2, person.Streak);
    }

    [Fact]
    public async Task CreateAnchor_StartsOpenPeriodToday()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");

        var outcome = await store.CreateAnchorAsync(sam, new AnchorRequest("Walk", "morning", "06:30", 62));

        var anchor = outcome.Value!;
        Assert.Equal(("morning", "06:30", 62, false), (anchor.Slot, anchor.TargetTime, anchor.Days, anchor.Retired));
        Assert.Equal([new AnchorPeriodDto(today, null, 62)], anchor.Periods);
    }

    [Theory]
    [InlineData("", "day", null)]
    [InlineData("Walk", "noon", null)]
    [InlineData("Walk", "day", "25:00")]
    public async Task CreateAnchor_BadInput_Invalid(string title, string slot, string? target)
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");

        Assert.Equal(AnchorResult.Invalid, (await store.CreateAnchorAsync(sam, new AnchorRequest(title, slot, target))).Result);
    }

    [Fact]
    public async Task CreateAnchor_TitleOver120Chars_Invalid()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");

        Assert.Equal(AnchorResult.Invalid, (await store.CreateAnchorAsync(sam, new AnchorRequest(new string('x', 121)))).Result);
    }

    [Fact]
    public async Task UpdateAnchor_DaysChange_SplitsPeriod()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");
        await backdateAsync(walk, today.AddDays(-10));

        Assert.Equal(AnchorResult.Ok, await store.UpdateAnchorAsync(walk, new AnchorRequest("Evening walk", Days: 62)));

        var anchor = (await store.GetDayAsync(sam))!.Anchors.Single();
        Assert.Equal("Evening walk", anchor.Title);
        Assert.Equal("day", anchor.Slot);
        Assert.Equal([new AnchorPeriodDto(today.AddDays(-10), today, DayMask.Every), new AnchorPeriodDto(today, null, 62)], anchor.Periods);
    }

    [Fact]
    public async Task UpdateAnchor_EmptyTargetTime_Clears()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var outcome = await store.CreateAnchorAsync(sam, new AnchorRequest("Up", "morning", "06:00"));

        await store.UpdateAnchorAsync(outcome.Value!.Id, new AnchorRequest("Up", TargetTime: string.Empty));

        Assert.Null((await store.GetDayAsync(sam))!.Anchors.Single().TargetTime);
    }

    [Fact]
    public async Task Retire_ThenRestore()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");
        await backdateAsync(walk, today.AddDays(-3));

        Assert.Equal(AnchorResult.Ok, await store.RetireAnchorAsync(walk));
        var retired = (await store.GetDayAsync(sam))!.Anchors.Single();
        Assert.True(retired.Retired);
        Assert.Equal("none", retired.Cells.Single().State);
        var person = (await store.GetDayAsync(sam))!;
        Assert.Equal(0, person.TodayTotal);

        Assert.Equal(AnchorResult.Ok, await store.RestoreAnchorAsync(walk));
        Assert.Equal("open", (await store.GetDayAsync(sam))!.Anchors.Single().Cells.Single().State);
    }

    [Fact]
    public async Task SetDone_StaleClientUpdatedAt_Ignored()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");

        await store.SetDoneAsync(walk, today, new AnchorLogRequest(true, noon));
        await store.SetDoneAsync(walk, today, new AnchorLogRequest(false, noon.AddMinutes(-5)));

        Assert.Equal("kept", (await store.GetDayAsync(sam))!.Anchors.Single().Cells.Single().State);
    }

    [Fact]
    public async Task SetDone_Undo_ClearsDoneAt()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");

        await store.SetDoneAsync(walk, today, new AnchorLogRequest(true));
        Assert.NotNull((await store.GetDayAsync(sam))!.Anchors.Single().Cells.Single().DoneAt);
        await store.SetDoneAsync(walk, today, new AnchorLogRequest(false));

        var cell = (await store.GetDayAsync(sam))!.Anchors.Single().Cells.Single();
        Assert.Equal(("open", (DateTimeOffset?)null), (cell.State, cell.DoneAt));
    }

    [Fact]
    public async Task SetDone_FutureDate_Invalid()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");

        Assert.Equal(AnchorResult.Ok, await store.SetDoneAsync(walk, today.AddDays(1), new AnchorLogRequest(true)));
        Assert.Equal(AnchorResult.Invalid, await store.SetDoneAsync(walk, today.AddDays(2), new AnchorLogRequest(true)));
    }

    [Fact]
    public async Task SetDone_UnknownAnchor_NotFound()
    {
        var store = await storeAsync();

        Assert.Equal(AnchorResult.NotFound, await store.SetDoneAsync("999", today, new AnchorLogRequest(true)));
        Assert.Equal(AnchorResult.NotFound, await store.SetDoneAsync("feed:abc", today, new AnchorLogRequest(true)));
    }

    [Fact]
    public async Task CreatePerson_BlankName_Invalid()
    {
        var store = await storeAsync();

        Assert.Equal(AnchorResult.Invalid, (await store.CreatePersonAsync(new AnchorPersonRequest("  "))).Result);
        Assert.Equal(AnchorResult.Invalid, (await store.CreatePersonAsync(new AnchorPersonRequest("Sam", "chartreuse"))).Result);
    }

    [Fact]
    public async Task CreatePerson_PicksDistinctColors()
    {
        var store = await storeAsync();

        var first = (await store.CreatePersonAsync(new AnchorPersonRequest("Sam"))).Value!;
        var second = (await store.CreatePersonAsync(new AnchorPersonRequest("Emma"))).Value!;

        Assert.NotEqual(first.Color, second.Color);
    }

    [Fact]
    public async Task ArchivePerson_HidesFromBoard()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");

        Assert.Equal(AnchorResult.Ok, await store.ArchivePersonAsync(sam));

        Assert.Empty((await store.GetBoardAsync()).People);
    }

    [Fact]
    public async Task OrderPeopleAndAnchors()
    {
        var store = await storeAsync();
        var sam = await personAsync(store, "Sam");
        var emma = await personAsync(store, "Emma");
        var walk = await anchorAsync(store, sam, "Walk");
        var read = await anchorAsync(store, sam, "Read");

        await store.OrderPeopleAsync(new AnchorOrderRequest([emma, sam]));
        await store.OrderAnchorsAsync(sam, new AnchorOrderRequest([read, walk]));

        var board = await store.GetBoardAsync();
        Assert.Equal(["Emma", "Sam"], board.People.Select(p => p.Name));
        Assert.Equal(["Read", "Walk"], AnchorView.Ordered(board.People[1].Anchors).Select(a => a.Title));
    }

    [Fact]
    public async Task Mutations_RaiseChanged()
    {
        var store = await storeAsync();
        var raised = 0;
        notifier.Changed += () => raised++;

        var sam = await personAsync(store, "Sam");
        var walk = await anchorAsync(store, sam, "Walk");
        await store.SetDoneAsync(walk, today, new AnchorLogRequest(true));

        Assert.Equal(3, raised);
    }

    [Fact]
    public async Task TwoCircuits_EditSeesTheOthersChange()
    {
        var first = await storeAsync();
        var sam = await personAsync(first, "Sam");
        var walk = await anchorAsync(first, sam, "Walk");
        await backdateAsync(walk, today.AddDays(-5));
        await first.RetireAnchorAsync(walk);

        // Another page (its own DbContext) restores it...
        await using var otherDb = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        var second = new LocalAnchorStore(otherDb, new FixedTimeProvider(noon, TimeZoneInfo.Utc), notifier);
        await second.RestoreAnchorAsync(walk);

        // ...and this one, which retired it, now retires it again: it must see the restored anchor, not its own stale copy.
        await first.RetireAnchorAsync(walk);

        Assert.True((await second.GetDayAsync(sam))!.Anchors.Single().Retired);
    }
}
