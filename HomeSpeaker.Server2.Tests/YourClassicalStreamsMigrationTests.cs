using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HomeSpeaker.Server2.Tests;

public sealed class YourClassicalStreamsMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261116012205_AddBluetoothSensorReadings";

    private readonly TempDirectory directory = new();

    [Fact]
    public async Task FreshDatabase_HasTheCurrentYourClassicalStreams()
    {
        await using var db = createContext();
        await db.Database.MigrateAsync();

        var streams = await db.RadioStreams.ToDictionaryAsync(s => s.Name, s => s.Url);

        Assert.DoesNotContain("Hygge", streams.Keys);
        Assert.DoesNotContain("Focus", streams.Keys);
        Assert.Equal("https://holiday.stream.publicradio.org/holiday.aac", streams["Holiday Stream"]);
        Assert.Equal("https://favorites.stream.publicradio.org/favorites.aac", streams["Essentials"]);
        Assert.Equal("https://lullabies.stream.publicradio.org/lullabies.aac", streams["Sleep"]);
        Assert.Equal("https://classicalkids.stream.publicradio.org/classicalkids.aac", streams["YourClassical Children"]);
        Assert.Equal("https://ycradio.stream.publicradio.org/ycradio.aac", streams["YourClassical Radio"]);
        Assert.Equal("https://celebratethemaestro.stream.publicradio.org/celebratethemaestro.aac", streams["Celebrate the Maestro"]);
        Assert.Equal("https://hispanicheritage.stream.publicradio.org/hispanicheritage.aac", streams["Hispanic Heritage Month"]);
        Assert.Equal("https://pixelplot.stream.publicradio.org/pixelplot.aac", streams["Pixel & Plot"]);
        Assert.Equal(24, streams.Count);
    }

    [Fact]
    public async Task ExistingDatabase_KeepsHouseholdEditsAndDropsAutoplayOfRemovedStreams()
    {
        await using var db = createContext();
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var hygge = await db.RadioStreams.SingleAsync(s => s.Name == "Hygge");
        var lullabies = await db.RadioStreams.SingleAsync(s => s.Name == "Lullabies");
        lullabies.Name = "Bedtime";
        db.RadioStreams.Add(new RadioStream { Name = "My Pixel", Url = "https://pixelplot.stream.publicradio.org/pixelplot.mp3", CreatedAt = DateTime.UtcNow });
        var settings = new AutoPlaySettingsEntity();
        db.AutoPlaySettings.Add(settings);
        await db.SaveChangesAsync();
        db.AutoPlaySources.Add(new AutoPlaySourceEntity { AutoPlaySettingsId = settings.Id, SourceType = Models.AutoPlaySourceType.RadioStream, RadioStreamId = hygge.Id });
        db.AutoPlaySources.Add(new AutoPlaySourceEntity { AutoPlaySettingsId = settings.Id, SourceType = Models.AutoPlaySourceType.RadioStream, RadioStreamId = lullabies.Id });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var names = await db.RadioStreams.Select(s => s.Name).ToListAsync();
        Assert.Contains("Bedtime", names);
        Assert.DoesNotContain("Sleep", names);
        Assert.Contains("My Pixel", names);
        Assert.DoesNotContain("Pixel & Plot", names);
        Assert.Contains("Celebrate the Maestro", names);
        Assert.Equal([lullabies.Id], await db.AutoPlaySources.Select(s => s.RadioStreamId).ToListAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private MusicContext createContext() =>
        new(new DbContextOptionsBuilder<MusicContext>()
            .UseSqlite($"Data Source={Path.Combine(directory.Path, "test.db")}")
            .Options);
}
