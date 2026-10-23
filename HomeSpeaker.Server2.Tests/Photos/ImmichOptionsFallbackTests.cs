using HomeSpeaker.Server2.Services.Photos;
using HomeSpeaker.Server2.Settings;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class ImmichOptionsFallbackTests : IDisposable
{
    private const string EnvPrefix = "HSTEST_IMMICHFALLBACK_";
    private readonly TempDirectory dir = new();
    private readonly List<string> envVars = [];

    public void Dispose()
    {
        foreach (var name in envVars)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        dir.Dispose();
    }

    private void setEnv(string key, string value)
    {
        var name = EnvPrefix + key.Replace(":", "__", StringComparison.Ordinal);
        Environment.SetEnvironmentVariable(name, value);
        envVars.Add(name);
    }

    /// <summary>The app's order: appsettings JSON, then the database settings, then environment variables.</summary>
    private IConfigurationRoot buildWithDatabase(IDictionary<string, string?> db)
    {
        var connectionString = $"Data Source={Path.Combine(dir.Path, "s.db")};Pooling=False";
        new SqliteSettingsStore(connectionString).Save(db.ToDictionary(p => p.Key, p => p.Value));
        var jsonPath = Path.Combine(dir.Path, "appsettings.json");
        File.WriteAllText(jsonPath, "{}");
        return new ConfigurationBuilder()
            .AddJsonFile(jsonPath)
            .AddEnvironmentVariables(EnvPrefix)
            .AddDatabaseSettings(connectionString)
            .Build();
    }

    private static ImmichOptions bind(IConfiguration config)
    {
        var options = new ImmichOptions();
        config.GetSection("Immich").Bind(options);
        ImmichOptionsFallback.Apply(options, config);
        return options;
    }

    [Fact]
    public void Apply_EnvAlias_BeatsDatabaseSavedKey()
    {
        setEnv("IMMICH_API_KEY", "env-key");
        var config = buildWithDatabase(new Dictionary<string, string?> { ["Immich:ApiKey"] = "db-key" });

        Assert.Equal("env-key", bind(config).ApiKey);
        Assert.Equal("env-key", ImmichOptionsFallback.EffectiveApiKey(config));
    }

    [Fact]
    public void Apply_EnvSectionKey_BeatsEnvAlias()
    {
        setEnv("IMMICH_API_KEY", "alias-key");
        setEnv("Immich:ApiKey", "section-key");
        var config = buildWithDatabase(new Dictionary<string, string?> { ["Immich:ApiKey"] = "db-key" });

        Assert.Equal("section-key", bind(config).ApiKey);
        Assert.Equal("section-key", ImmichOptionsFallback.EffectiveApiKey(config));
    }

    [Fact]
    public void Apply_NoAlias_UsesDatabaseSavedKey()
    {
        var config = buildWithDatabase(new Dictionary<string, string?> { ["Immich:ApiKey"] = "db-key" });

        Assert.Equal("db-key", bind(config).ApiKey);
        Assert.Equal("db-key", ImmichOptionsFallback.EffectiveApiKey(config));
    }

    [Fact]
    public void Apply_UsesImmichApiKey_WhenSectionKeyMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "IMMICH_API_KEY", "abc" } })
            .Build();
        var options = new ImmichOptions();

        ImmichOptionsFallback.Apply(options, config);

        Assert.Equal("abc", options.ApiKey);
    }

    [Fact]
    public void Apply_KeepsSectionKey_WhenBothSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "IMMICH_API_KEY", "abc" } })
            .Build();
        var options = new ImmichOptions { ApiKey = "section" };

        ImmichOptionsFallback.Apply(options, config);

        Assert.Equal("section", options.ApiKey);
    }

    [Fact]
    public void Apply_LeavesNull_WhenNeitherSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var options = new ImmichOptions();

        ImmichOptionsFallback.Apply(options, config);

        Assert.Null(options.ApiKey);
    }
}
