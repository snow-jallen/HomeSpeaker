using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class AnchorProgressTests : IAsyncDisposable
{
    // Noon UTC, and the fixed provider's local zone is UTC, so "today" is 2026-10-13.
    private static readonly DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly today = new(2026, 10, 13);

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private MusicContext? dbContext;

    public async ValueTask DisposeAsync()
    {
        if (dbContext is not null)
        {
            await dbContext.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    private async Task<AnchorService> createServiceAsync()
    {
        await connection.OpenAsync();
        dbContext = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        return new AnchorService(dbContext, NullLogger<AnchorService>.Instance, new NoNotifications(), new FixedTimeProvider(now));
    }

    private void add(string user, DateOnly date, bool done) =>
        dbContext!.DailyAnchors.Add(new DailyAnchorEntity { UserId = user, Date = date, IsCompleted = done, AnchorName = "a" });

    [Fact]
    public async Task CountsTodaysCompletedAndTotalPerPerson_OrderedByName()
    {
        var service = await createServiceAsync();
        add("Sam", today, true);
        add("Sam", today, true);
        add("Emma", today, true);
        add("Emma", today, false);
        add("Emma", today, false);
        await dbContext!.SaveChangesAsync();

        var progress = await service.GetTodayProgressAsync();

        Assert.Equal([new AnchorProgress("Emma", 1, 3), new AnchorProgress("Sam", 2, 2)], progress);
        Assert.True(progress[1].IsComplete);
        Assert.False(progress[0].IsComplete);
    }

    [Fact]
    public async Task IgnoresOtherDays()
    {
        var service = await createServiceAsync();
        add("Emma", today.AddDays(-1), true);
        add("Emma", today.AddDays(1), true);
        await dbContext!.SaveChangesAsync();

        Assert.Empty(await service.GetTodayProgressAsync());
    }

    [Fact]
    public void Fraction_IsDoneOverTotal()
    {
        Assert.Equal(0.6, new AnchorProgress("x", 3, 5).Fraction, 3);
        Assert.Equal(0, new AnchorProgress("x", 0, 0).Fraction);
    }

    private sealed class NoNotifications : IAnchorNotificationService
    {
        public Task NotifyAnchorDefinitionCreated(AnchorDefinition anchorDefinition) => Task.CompletedTask;
        public Task NotifyAnchorDefinitionUpdated(AnchorDefinition anchorDefinition) => Task.CompletedTask;
        public Task NotifyAnchorDefinitionDeactivated(int anchorDefinitionId) => Task.CompletedTask;
        public Task NotifyUserAnchorAssigned(UserAnchor userAnchor) => Task.CompletedTask;
        public Task NotifyUserAnchorRemoved(string userId, int anchorDefinitionId) => Task.CompletedTask;
        public Task NotifyDailyAnchorCompletionUpdated(int dailyAnchorId, bool isCompleted, DateTime? completedAt) => Task.CompletedTask;
    }
}
