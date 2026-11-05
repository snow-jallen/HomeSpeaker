using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorStoreSelectorTests
{
    /// <summary>
    /// A Blazor page keeps one scope for as long as it is open, so the source has to be read on every call: switching to
    /// another HomeSpeaker in settings must reach an already-open kiosk page.
    /// </summary>
    [Fact]
    public async Task SourceChange_ReachesAnAlreadyResolvedStore()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var configuration = new ConfigurationManager();
        configuration["Anchors:Source"] = "local";

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AnchorChangeNotifier>();
        services.AddSingleton<RemoteAnchorCache>();
        services.AddDbContext<MusicContext>(o => o.UseSqlite(connection));
        services.AddScoped<LocalAnchorStore>();
        services.AddScoped<RemoteAnchorStore>();
        services.AddScoped<IAnchorStore, AnchorStoreSelector>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MusicContext>().Database.EnsureCreatedAsync();

        var store = scope.ServiceProvider.GetRequiredService<IAnchorStore>();
        await store.CreatePersonAsync(new AnchorPersonRequest("Sam"));
        Assert.Equal("Sam", Assert.Single((await store.GetBoardAsync()).People).Name);

        configuration["Anchors:Source"] = "remote";
        configuration["Anchors:RemoteUrl"] = "http://127.0.0.1:9/";

        await Assert.ThrowsAsync<AnchorSourceUnavailableException>(() => store.GetBoardAsync());
    }
}
