using System.Net;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorFeedOutboxTests : IAsyncDisposable
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly FakeFeedServer server = new();
    private readonly ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private MusicContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    private async Task<MusicContext> dbAsync()
    {
        await connection.OpenAsync();
        db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private HttpAnchorFeed feed() => new(
        server,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Anchors:Feed:Url"] = "https://feed.example/" }).Build(),
        clock,
        NullLogger<HttpAnchorFeed>.Instance);

    [Fact]
    public async Task Enqueue_ReplacesSameAnchorAndDate()
    {
        var context = await dbAsync();

        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, false, clock.GetUtcNow().AddSeconds(5), clock, CancellationToken.None);
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today.AddDays(-1), true, clock.GetUtcNow(), clock, CancellationToken.None);

        var pending = await AnchorFeedOutbox.PendingAsync(context, CancellationToken.None);
        Assert.Equal(2, pending.Count);
        Assert.False(pending[(FeedAnchorMerge.WireId("h1"), today)]);
    }

    [Fact]
    public async Task SendDue_Sent_RemovesRow()
    {
        var context = await dbAsync();
        server.Respond(HttpStatusCode.NoContent, string.Empty);
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);

        var done = await AnchorFeedOutbox.SendDueAsync(context, feed(), clock, CancellationToken.None);

        Assert.Equal(1, done);
        Assert.Empty(await AnchorFeedOutbox.PendingAsync(context, CancellationToken.None));
        Assert.Equal("PUT", server.Requests.Single().Method);
    }

    [Fact]
    public async Task SendDue_Rejected_RemovesRow()
    {
        var context = await dbAsync();
        server.Respond(HttpStatusCode.NotFound, string.Empty);
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);

        Assert.Equal(1, await AnchorFeedOutbox.SendDueAsync(context, feed(), clock, CancellationToken.None));
        Assert.Empty(await AnchorFeedOutbox.PendingAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task SendDue_Failed_KeepsRowAndBacksOff()
    {
        var context = await dbAsync();
        server.Fail();
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);
        var client = feed();

        Assert.Equal(0, await AnchorFeedOutbox.SendDueAsync(context, client, clock, CancellationToken.None));
        var row = await context.AnchorFeedOutbox.SingleAsync();
        Assert.Equal(1, row.Attempts);
        Assert.Equal(clock.GetUtcNow().AddSeconds(10), row.NextAttemptAt);
        Assert.NotNull(row.LastError);

        // Not due yet: nothing is sent.
        server.Respond(HttpStatusCode.NoContent, string.Empty);
        Assert.Equal(0, await AnchorFeedOutbox.SendDueAsync(context, client, clock, CancellationToken.None));
        Assert.Empty(server.Requests);

        // Feed back and the wait over: it goes.
        clock.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal(1, await AnchorFeedOutbox.SendDueAsync(context, client, clock, CancellationToken.None));
        Assert.Empty(await AnchorFeedOutbox.PendingAsync(context, CancellationToken.None));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(5, 160)]
    [InlineData(6, 300)]
    [InlineData(30, 300)]
    public void Backoff_DoublesToFiveMinutes(int attempts, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), AnchorFeedOutbox.Backoff(attempts));

    [Fact]
    public async Task SendDue_NewerTickDuringSend_IsKept()
    {
        var context = await dbAsync();
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);

        // While the feed is still answering the first tick, the person taps Undo (another page, its own DbContext).
        server.Respond(HttpStatusCode.NoContent, string.Empty);
        server.DuringRequest = async () =>
        {
            server.DuringRequest = null;
            await using var other = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
            await AnchorFeedOutbox.EnqueueAsync(other, "h1", today, false, clock.GetUtcNow().AddSeconds(3), clock, CancellationToken.None);
        };

        await AnchorFeedOutbox.SendDueAsync(context, feed(), clock, CancellationToken.None);

        var pending = await AnchorFeedOutbox.PendingAsync(context, CancellationToken.None);
        Assert.False(Assert.Single(pending).Value);
    }

    [Fact]
    public async Task SendDue_NewerTickDuringFailedSend_KeepsItDueNow()
    {
        var context = await dbAsync();
        await AnchorFeedOutbox.EnqueueAsync(context, "h1", today, true, clock.GetUtcNow(), clock, CancellationToken.None);
        server.Respond(HttpStatusCode.InternalServerError, string.Empty);
        server.DuringRequest = async () =>
        {
            server.DuringRequest = null;
            await using var other = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
            await AnchorFeedOutbox.EnqueueAsync(other, "h1", today, false, clock.GetUtcNow().AddSeconds(3), clock, CancellationToken.None);
        };

        await AnchorFeedOutbox.SendDueAsync(context, feed(), clock, CancellationToken.None);

        var row = await context.AnchorFeedOutbox.AsNoTracking().SingleAsync();
        Assert.Equal((false, 0, clock.GetUtcNow()), (row.Done, row.Attempts, row.NextAttemptAt));
    }
}
