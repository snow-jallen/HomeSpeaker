using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class LegacyAnchorImporterTests : IAsyncDisposable
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private MusicContext? db;

    public async ValueTask DisposeAsync()
    {
        if (db is not null)
        {
            await db.DisposeAsync();
        }

        await connection.DisposeAsync();
    }

    /// <summary>A database as an existing install has it: every migration applied, so the old tables still exist.</summary>
    private async Task<MusicContext> migratedAsync()
    {
        await connection.OpenAsync();
        db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        return db;
    }

    private async Task seedLegacyAsync(MusicContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO AnchorDefinitions (Id, Name, Description, IsActive, CreatedAt) VALUES
                (1, 'Read scriptures', '', 1, '2026-09-01 00:00:00'),
                (2, 'Old habit', '', 0, '2026-09-01 00:00:00');
            INSERT INTO UserAnchors (UserId, AnchorDefinitionId, CreatedAt) VALUES
                ('Sam', 1, '2026-09-01 12:00:00');
            INSERT INTO DailyAnchors (UserId, AnchorDefinitionId, Date, IsCompleted, CompletedAt, AnchorName, AnchorDescription, CreatedAt) VALUES
                ('Sam', 1, '2026-11-02', 1, '2026-11-02 14:00:00', 'Read scriptures', '', '2026-11-02 00:00:00'),
                ('Sam', 1, '2026-11-03', 0, NULL, 'Read scriptures', '', '2026-11-03 00:00:00'),
                ('Emma', 2, '2026-10-01', 1, '2026-10-01 09:00:00', 'Old habit', '', '2026-10-01 00:00:00');
            """);
    }

    private static async Task<bool> tableExistsAsync(MusicContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.Add(new SqliteParameter("$name", table));
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    [Fact]
    public async Task Import_CreatesPeopleAnchorsLogs_AndDropsLegacyTables()
    {
        var context = await migratedAsync();
        await seedLegacyAsync(context);

        var created = await LegacyAnchorImporter.ImportAsync(context, today, CancellationToken.None);

        Assert.Equal(2, created);
        var people = await context.AnchorPeople.OrderBy(p => p.SortOrder).ToListAsync();
        Assert.Equal(["Emma", "Sam"], people.Select(p => p.Name));
        Assert.All(people, p => Assert.True(AnchorColors.IsValid(p.Color)));

        var sam = await context.Anchors.Include(a => a.Periods).Include(a => a.Logs).SingleAsync(a => a.PersonId == people[1].Id);
        Assert.Equal("Read scriptures", sam.Title);
        Assert.Equal(AnchorSlot.Day, sam.Slot);
        var period = Assert.Single(sam.Periods);
        Assert.Equal((new DateOnly(2026, 11, 2), (DateOnly?)null, DayMask.Every), (period.From, period.To, period.Days));
        var log = Assert.Single(sam.Logs);
        Assert.Equal(new DateOnly(2026, 11, 2), log.Date);
        Assert.True(log.Done);

        var emma = await context.Anchors.Include(a => a.Periods).SingleAsync(a => a.PersonId == people[0].Id);
        Assert.All(emma.Periods, p => Assert.NotNull(p.To));

        Assert.False(await tableExistsAsync(context, "DailyAnchors"));
        Assert.False(await tableExistsAsync(context, "UserAnchors"));
        Assert.False(await tableExistsAsync(context, "AnchorDefinitions"));
    }

    [Fact]
    public async Task Import_SecondRun_IsNoOp()
    {
        var context = await migratedAsync();
        await seedLegacyAsync(context);
        await LegacyAnchorImporter.ImportAsync(context, today, CancellationToken.None);

        Assert.Equal(0, await LegacyAnchorImporter.ImportAsync(context, today, CancellationToken.None));
        Assert.Equal(2, await context.AnchorPeople.CountAsync());
    }

    [Fact]
    public async Task Import_EmptyLegacyTables_DropsThemAndCreatesNothing()
    {
        var context = await migratedAsync();

        Assert.Equal(0, await LegacyAnchorImporter.ImportAsync(context, today, CancellationToken.None));
        Assert.False(await tableExistsAsync(context, "DailyAnchors"));
        Assert.Equal(0, await context.AnchorPeople.CountAsync());
    }

    [Fact]
    public async Task Import_NoLegacyTables_ReturnsZero()
    {
        await connection.OpenAsync();
        db = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        Assert.Equal(0, await LegacyAnchorImporter.ImportAsync(db, today, CancellationToken.None));
    }
}
