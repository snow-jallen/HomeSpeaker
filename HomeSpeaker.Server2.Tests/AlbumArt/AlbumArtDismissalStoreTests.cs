using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.AlbumArt;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public sealed class AlbumArtDismissalStoreTests : IDisposable
{
    private readonly TempDirectory directory = new();

    private string connectionString => $"Data Source={Path.Combine(directory.Path, "test.db")}";

    [Fact]
    public async Task HideAsync_PersistsAcrossRestarts()
    {
        var group = new AlbumArtGroup(AlbumArtGroup.KeyFor("Queen", "Jazz", "/m/1.mp3"), "Queen", "Jazz", [new AlbumArtSong("/m/1.mp3", "Mustapha")]);
        await using (var first = await createProvider())
        {
            var store = first.GetRequiredService<AlbumArtDismissalStore>();
            await store.HideAsync(group, CancellationToken.None);
            await store.HideAsync(group, CancellationToken.None);
        }

        await using var second = await createProvider();
        var keys = await second.GetRequiredService<AlbumArtDismissalStore>().GetHiddenKeysAsync(CancellationToken.None);

        Assert.Equal([group.Key], keys);
        using var scope = second.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<MusicContext>().AlbumArtDismissals.SingleAsync();
        Assert.Equal("Queen", row.Artist);
        Assert.Equal("Jazz", row.Name);
    }

    [Fact]
    public async Task UnhideAsync_RemovesTheKey()
    {
        var group = new AlbumArtGroup("song:/m/1.mp3", "Queen", null, [new AlbumArtSong("/m/1.mp3", "Mustapha")]);
        await using var provider = await createProvider();
        var store = provider.GetRequiredService<AlbumArtDismissalStore>();
        await store.HideAsync(group, CancellationToken.None);

        await store.UnhideAsync(group.Key, CancellationToken.None);

        Assert.Empty(await store.GetHiddenKeysAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private async Task<ServiceProvider> createProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MusicContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AlbumArtDismissalStore>();
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MusicContext>().Database.MigrateAsync();
        return provider;
    }
}
