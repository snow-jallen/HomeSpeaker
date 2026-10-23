using System.Net;
using HomeSpeaker.Server2.Settings;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class AdminPinResetTests : IDisposable
{
    private readonly TempDirectory dir = new();

    private SqliteSettingsStore newStore() => new($"Data Source={Path.Combine(dir.Path, "s.db")};Pooling=False");

    private static IConfiguration config(string? reset) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [AdminPinReset.ConfigKey] = reset }).Build();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void Apply_ResetTrue_DeletesPinOnly()
    {
        var store = newStore();
        store.Save(new Dictionary<string, string?> { [AdminPin.ConfigKey] = AdminPin.Hash("1234"), ["Immich:BaseUrl"] = "http://x" });

        var applied = AdminPinReset.Apply(config("true"), store, null, NullLogger.Instance);

        Assert.True(applied);
        var all = store.LoadAll();
        Assert.False(all.ContainsKey(AdminPin.ConfigKey));
        Assert.Equal("http://x", all["Immich:BaseUrl"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("")]
    public void Apply_ResetNotTrue_LeavesPin(string? reset)
    {
        var store = newStore();
        store.Save(new Dictionary<string, string?> { [AdminPin.ConfigKey] = "hash" });

        Assert.False(AdminPinReset.Apply(config(reset), store, null, NullLogger.Instance));
        Assert.Equal("hash", store.LoadAll()[AdminPin.ConfigKey]);
    }

    [Fact]
    public void Apply_NoStore_IsNoOp()
    {
        Assert.False(AdminPinReset.Apply(config("true"), null, null, NullLogger.Instance));
    }

    [Fact]
    public async Task App_ResetTrue_OpensAdminPages()
    {
        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { [AdminPinReset.ConfigKey] = "true" });
        new SqliteSettingsStore($"Data Source={factory.DatabasePath};Pooling=False")
            .Save(new Dictionary<string, string?> { [AdminPin.ConfigKey] = AdminPin.Hash("1234") });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task App_ResetUnset_PinStillRequired()
    {
        using var factory = new TestServerFactory();
        new SqliteSettingsStore($"Data Source={factory.DatabasePath};Pooling=False")
            .Save(new Dictionary<string, string?> { [AdminPin.ConfigKey] = AdminPin.Hash("1234") });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri("/admin-check", UriKind.Relative));

        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
    }
}
