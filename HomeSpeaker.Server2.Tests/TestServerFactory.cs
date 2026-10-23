using HomeSpeaker.Server2.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HomeSpeaker.Server2.Tests;

/// <summary>
/// Boots the real HomeSpeaker app in-process for endpoint tests. Each instance gets its own temp
/// MediaFolder and SQLite file (deleted on dispose) and accepts in-memory config overrides through
/// <see cref="WithSettings"/>, e.g. <c>new TestServerFactory().WithSettings(new Dictionary&lt;string, string?&gt; { ["Features:Anchors"] = "true" })</c>.
/// <para>
/// Overrides are applied with <c>UseSetting</c> so they are visible to the config reads Program.cs does
/// while it registers services (feature flags decide which workers and endpoints exist). They sit below
/// environment variables, so the tests assume the machine has none of the feature env vars set.
/// </para>
/// <para>
/// Every hosted service except <see cref="MigrationApplier"/> is removed: the rest drive audio hardware
/// (AirPlay, volume monitor, autoplay), reach the network (Immich sync and prefetch, APNs pushes, AI
/// analysis) or run long polling loops, which makes tests slow and flaky. The migration applier is
/// kept because the endpoints need the database schema.
/// </para>
/// </summary>
public sealed class TestServerFactory : WebApplicationFactory<Program>
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), "homespeaker-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> settings = new(StringComparer.OrdinalIgnoreCase);

    public TestServerFactory()
    {
        Directory.CreateDirectory(rootDirectory);
        MediaFolder = Path.Combine(rootDirectory, "media");
        Directory.CreateDirectory(MediaFolder);
        DatabasePath = Path.Combine(rootDirectory, "test.db");
        settings[ConfigKeys.MediaFolder] = MediaFolder;
        settings["SqliteConnectionString"] = $"Data Source={DatabasePath}";
    }

    public string MediaFolder { get; }

    public string DatabasePath { get; }

    /// <summary>Adds or replaces config values for this instance. Call before the first request.</summary>
    public TestServerFactory WithSettings(IDictionary<string, string?> overrides)
    {
        foreach (var pair in overrides)
        {
            settings[pair.Key] = pair.Value;
        }

        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var pair in settings)
        {
            builder.UseSetting(pair.Key, pair.Value);
        }

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType != typeof(MigrationApplier))
                .ToList())
            {
                services.Remove(descriptor);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(rootDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a lingering handle must not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: temp files are cleaned by the OS eventually.
        }
    }
}
