using System.Net;
using System.Net.Http.Headers;
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
    private HttpClient httpClient = new();
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
    public async Task ReplacingSharedArtOnOneStreamKeepsTheFileForTheOther()
    {
        var service = await createServiceAsync();
        writeFavicon("shared.png");
        writeFavicon("hd2.png");
        var kbaq = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "shared.png");
        var hd2 = await service.CreateStreamAsync("KBAQ HD2", "http://example.com/hd2", faviconFileName: "shared.png");

        await service.UpdateStreamAsync(hd2.Id, "KBAQ HD2", "http://example.com/hd2", faviconFileName: "hd2.png");

        Assert.True(File.Exists(Path.Combine(faviconsDirectory, "shared.png")));
        Assert.Equal("shared.png", (await service.GetStreamByIdAsync(kbaq.Id))!.FaviconFileName);
    }

    [Fact]
    public async Task DeletingAStreamKeepsArtAnotherStreamStillUses()
    {
        var service = await createServiceAsync();
        writeFavicon("shared.png");
        var kbaq = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "shared.png");
        var hd2 = await service.CreateStreamAsync("KBAQ HD2", "http://example.com/hd2", faviconFileName: "shared.png");

        await service.DeleteStreamAsync(hd2.Id);
        Assert.True(File.Exists(Path.Combine(faviconsDirectory, "shared.png")));

        await service.DeleteStreamAsync(kbaq.Id);
        Assert.False(File.Exists(Path.Combine(faviconsDirectory, "shared.png")));
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
    public async Task DownloadedImageIsSavedUnderAUniqueName()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        var service = await createServiceAsync(respondWith(png, "image/png"));

        var first = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconUrl: "http://images.example/kbaq.png");
        var second = await service.CreateStreamAsync("kbaq", "http://example.com/kbaq2", faviconUrl: "http://images.example/kbaq.png");

        Assert.NotNull(first.FaviconFileName);
        Assert.StartsWith("kbaq", first.FaviconFileName, StringComparison.Ordinal);
        Assert.EndsWith(".png", first.FaviconFileName, StringComparison.Ordinal);
        Assert.NotEqual(first.FaviconFileName, second.FaviconFileName);
        Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(faviconsDirectory, first.FaviconFileName)));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("image/svg+xml")]
    [InlineData(null)]
    public async Task NonImageDownloadIsNotSaved(string? contentType)
    {
        var service = await createServiceAsync(respondWith("<html>router admin</html>"u8.ToArray(), contentType));

        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconUrl: "http://192.168.1.1/");

        Assert.Null(stream.FaviconFileName);
        Assert.Empty(faviconFiles());
    }

    [Fact]
    public async Task DownloadDeclaredOverTheLimitIsNotSaved()
    {
        var service = await createServiceAsync(respondWith(new byte[RadioStreamService.MaxFaviconBytes + 1], "image/png"));

        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconUrl: "http://images.example/huge.png");

        Assert.Null(stream.FaviconFileName);
        Assert.Empty(faviconFiles());
    }

    [Fact]
    public async Task DownloadOverTheLimitWithoutContentLengthIsNotSaved()
    {
        var content = new UnknownLengthContent(new byte[(RadioStreamService.MaxFaviconBytes * 3) / 2]);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        Assert.Null(content.Headers.ContentLength); // the size limit has to be enforced while reading
        var service = await createServiceAsync(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconUrl: "http://images.example/endless.png");

        Assert.Null(stream.FaviconFileName);
        Assert.Empty(faviconFiles());
    }

    [Fact]
    public async Task UpdateWithANonImageUrlKeepsTheCurrentArt()
    {
        var service = await createServiceAsync(respondWith("not an image"u8.ToArray(), "text/plain"));
        writeFavicon("kbaq.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "kbaq.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ", "http://example.com/kbaq", faviconUrl: "http://localhost:18888/secret");

        Assert.Equal("kbaq.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
        Assert.Equal(["kbaq.png"], faviconFiles());
    }

    [Fact]
    public async Task UpdateWithADownloadedImageReplacesTheOldFile()
    {
        var service = await createServiceAsync(respondWith([1, 2, 3], "image/webp"));
        writeFavicon("old.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "old.png");

        await service.UpdateStreamAsync(stream.Id, "KBAQ", "http://example.com/kbaq", faviconUrl: "http://images.example/kbaq.webp");

        var saved = (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName;
        Assert.NotNull(saved);
        Assert.EndsWith(".webp", saved, StringComparison.Ordinal);
        Assert.Equal([saved], faviconFiles());
    }

    [Fact]
    public async Task FailedSaveWithANewFileNameKeepsTheOldFile()
    {
        var service = await createServiceAsync();
        writeFavicon("old.png");
        writeFavicon("new.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "old.png");

        // A null name violates the NOT NULL constraint, so the save throws
        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            service.UpdateStreamAsync(stream.Id, null!, "http://example.com/kbaq", faviconFileName: "new.png"));

        dbContext!.ChangeTracker.Clear();
        Assert.True(File.Exists(Path.Combine(faviconsDirectory, "old.png")));
        Assert.Equal("old.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
    }

    [Fact]
    public async Task FailedSaveWithADownloadedImageKeepsTheOldFileAndDropsTheDownload()
    {
        var service = await createServiceAsync(respondWith([1, 2, 3], "image/png"));
        writeFavicon("old.png");
        var stream = await service.CreateStreamAsync("KBAQ", "http://example.com/kbaq", faviconFileName: "old.png");

        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            service.UpdateStreamAsync(stream.Id, null!, "http://example.com/kbaq", faviconUrl: "http://images.example/kbaq.png"));

        dbContext!.ChangeTracker.Clear();
        Assert.Equal(["old.png"], faviconFiles());
        Assert.Equal("old.png", (await service.GetStreamByIdAsync(stream.Id))!.FaviconFileName);
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

    private static FakeHandler respondWith(byte[] body, string? contentType) => new(_ =>
    {
        var content = new ByteArrayContent(body);
        if (contentType is not null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    });

    private string[] faviconFiles() => Directory.Exists(faviconsDirectory)
        ? Directory.GetFiles(faviconsDirectory).Select(f => Path.GetFileName(f)!).ToArray()
        : [];

    private async Task<RadioStreamService> createServiceAsync(HttpMessageHandler? handler = null)
    {
        if (handler is not null)
        {
            httpClient.Dispose();
            httpClient = new HttpClient(handler);
        }

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

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    // A body whose length is unknown up front, like a chunked response.
    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(body, 0, body.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
