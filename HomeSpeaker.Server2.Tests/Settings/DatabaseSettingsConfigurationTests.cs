using HomeSpeaker.Server2.Settings;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class DatabaseSettingsConfigurationTests : IDisposable
{
    private readonly TempDirectory dir = new();
    private readonly List<string> envVars = [];

    private string ConnectionString => $"Data Source={Path.Combine(dir.Path, "s.db")};Pooling=False";

    public void Dispose()
    {
        foreach (var name in envVars)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        dir.Dispose();
    }

    private string SetEnv(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        envVars.Add(name);
        return name;
    }

    [Fact]
    public void Configuration_DatabaseValueOverridesAppsettings()
    {
        new SqliteSettingsStore(ConnectionString).Save(new Dictionary<string, string?> { ["Immich:BaseUrl"] = "db" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Immich:BaseUrl"] = "json", ["Other"] = "keep" })
            .AddDatabaseSettings(ConnectionString)
            .Build();

        Assert.Equal("db", config["immich:baseurl"]);
        Assert.Equal("keep", config["Other"]);
    }

    [Fact]
    public void Configuration_EnvVarOverridesDatabaseValue()
    {
        var prefix = "HSTEST" + Guid.NewGuid().ToString("N") + "_";
        SetEnv(prefix + "Immich__BaseUrl", "env");
        new SqliteSettingsStore(ConnectionString).Save(new Dictionary<string, string?> { ["Immich:BaseUrl"] = "db" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Immich:BaseUrl"] = "json" })
            .AddEnvironmentVariables(prefix)
            .AddDatabaseSettings(ConnectionString)
            .Build();

        Assert.Equal("env", config["Immich:BaseUrl"]);
    }

    [Fact]
    public void Configuration_EmptyEnvVarOverridesDatabaseValue()
    {
        var prefix = "HSTEST" + Guid.NewGuid().ToString("N") + "_";
        SetEnv(prefix + "Admin__PinHash", "");
        new SqliteSettingsStore(ConnectionString).Save(new Dictionary<string, string?> { ["Admin:PinHash"] = "abc" });

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables(prefix)
            .AddDatabaseSettings(ConnectionString)
            .Build();

        Assert.Equal("", config["Admin:PinHash"]);
    }

    [Fact]
    public void Configuration_MissingDatabaseDirectory_IsTolerated()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["A"] = "1" })
            .AddDatabaseSettings($"Data Source={Path.Combine(dir.Path, "nope", "s.db")};Pooling=False")
            .Build();

        Assert.Equal("1", config["A"]);
    }

    [Fact]
    public void AddDatabaseSettings_EmptyConnectionString_AddsNothing()
    {
        var builder = new ConfigurationBuilder();
        builder.AddDatabaseSettings("");
        builder.AddDatabaseSettings(null);
        Assert.Empty(builder.Sources);
    }

    [Fact]
    public void SettingsWriter_Save_ReloadsConfiguration()
    {
        var builder = new ConfigurationBuilder().AddDatabaseSettings(ConnectionString);
        var config = builder.Build();
        var source = builder.Sources.OfType<DatabaseSettingsConfigurationSource>().Single();
        ISettingsWriter writer = new SettingsWriter(source.Store, source.Provider);
        Assert.Null(config["Admin:Name"]);

        writer.Save(new Dictionary<string, string?> { ["Admin:Name"] = "x" });

        Assert.Equal("x", config["Admin:Name"]);
    }

    [Fact]
    public void SettingsWriter_NotConfigured_Throws()
    {
        ISettingsWriter writer = new SettingsWriter(null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => writer.Save(new Dictionary<string, string?>()));
        Assert.Equal("Database settings are not configured", ex.Message);
    }
}
