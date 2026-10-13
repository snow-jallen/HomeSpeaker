using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests;

public sealed class RadioStreamServiceTests : IAsyncDisposable
{
    private readonly string mediaFolder = Path.Combine(Path.GetTempPath(), "hs-radio-tests-" + Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly HttpClient httpClient = new();
    private MusicContext? dbContext;

    private string faviconsDirectory => Path.Combine(mediaFolder, "favicons");

    [Fact]
    public async Task UpdateWithTheSameFileNameKeepsTheFile()
    {
        var service = await createServiceAsync();
        writeFavicon("kbaq.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "kbaq.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ", "http://example.com/kbaq", faviconFileName: "kbaq.png");

        Assert.True(File.Exists(Path.Combine(faviconsDirectory, "kbaq.png")));
        Assert.Equal("kbaq.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
    }

    [Fact]
    public async Task UpdateWithANewFileNameReplacesTheOldFile()
    {
        var service = await createServiceAsync();
        writeFavicon("old.png");
        writeFavicon("new.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "old.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ", "http://example.com/kbaq", faviconFileName: "new.png");

        Assert.False(File.Exists(Path.Combine(faviconsDirectory, "old.png")));
        Assert.Equal("new.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
    }

    [Fact]
    public async Task UpdateWithoutArtLeavesItUnchanged()
    {
        var service = await createServiceAsync();
        writeFavicon("kbaq.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "kbaq.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ 89.5", "http://example.com/kbaq2");

        var updated = (await service.GetStreamByIdAsync(stream.Id))!;
        Assert.Equal("kbaq.png", updated.FaviconFileName);
        Assert.Equal("KBAQ 89.5", updated.Name);
    }

    [Fact]
    public async Task UpdateWithAnImageUrlThatFailsToDownloadKeepsTheCurrentArt()
    {
        var service = await createServiceAsync();
        writeFavicon("kbaq.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "kbaq.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ", "http://example.com/kbaq", faviconUrl: "http://127.0.0.1:1/logo.png");

        Assert.True(File.Exists(Path.Combine(faviconsDirectory, "kbaq.png")));
        Assert.Equal("kbaq.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
    }

    [Fact]
    public async Task CreateGivesEachNewStreamTheNextDisplayOrder()
    {
        var service = await createServiceAsync();

        var first = await service.CreateStreamAsync("One", "http://example.com/1");
        var second = await service.CreateStreamAsync("Two", "http://example.com/2");

        Assert.Equal(first.DisplayOrder + 1, second.DisplayOrder);
    }

    [Fact]
    public async Task FindStreamByNameIgnoresCase()
    {
        var service = await createServiceAsync();
        await service.CreateStreamAsync("Classical Kids", "http://example.com/ck", faviconFileName: "ck.png");

        var found = await service.FindStreamByNameAsync("classical KIDS");

        Assert.NotNull(found);
        Assert.Equal("ck.png", found.FaviconFileName);
        Assert.Null(await service.FindStreamByNameAsync("Classical"));
        Assert.Null(await service.FindStreamByNameAsync(null));
    }

    [Fact]
    public async Task FindStreamByNameSeesChangesAfterInvalidateCache()
    {
        var service = await createServiceAsync();
        Assert.Null(await service.FindStreamByNameAsync("Jazz"));

        dbContext!.RadioStreams.Add(new RadioStream { Name = "Jazz", Url = "http://example.com/jazz" });
        await dbContext.SaveChangesAsync();
        Assert.Null(await service.FindStreamByNameAsync("Jazz"));

        service.InvalidateCache();
        Assert.NotNull(await service.FindStreamByNameAsync("Jazz"));
    }

    public async ValueTask DisposeAsync()
    {
        if (dbContext is not null)
        {
            await dbContext.DisposeAsync();
        }

        await connection.DisposeAsync();
        cache.Dispose();
        httpClient.Dispose();
        if (Directory.Exists(mediaFolder))
        {
            Directory.Delete(mediaFolder, recursive: true);
        }
    }

    private async Task<RadioStreamService> createServiceAsync()
    {
        await connection.OpenAsync();
        dbContext = new MusicContext(new DbContextOptionsBuilder<MusicContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ConfigKeys.MediaFolder] = mediaFolder })
            .Build();
        return new RadioStreamService(dbContext, NullLogger<RadioStreamService>.Instance, configuration, cache, httpClient, TimeProvider.System);
    }

    private void writeFavicon(string fileName)
    {
        Directory.CreateDirectory(faviconsDirectory);
        File.WriteAllBytes(Path.Combine(faviconsDirectory, fileName), [1, 2, 3]);
    }
}
