using System.Net;
using System.Net.Http.Json;
using HomeSpeaker.Server2.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SettingsEndpointTests
{
    private static readonly Uri settingsUri = new("/api/settings", UriKind.Relative);

    private sealed record FieldDto(string Key, string Label, string? Value, bool IsSecret, bool IsOverridden);

    private sealed class FakeRestarter : IAppRestarter
    {
        public int Calls { get; private set; }

        public void Restart() => Calls++;
    }

    [Fact]
    public async Task PutSettings_WithPinNoCookie_Returns401or302()
    {
        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Admin:PinHash"] = AdminPin.Hash("1234") });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.PutAsJsonAsync(settingsUri, new Dictionary<string, string?> { [SettingKeys.ForecastLatitude] = "40" });

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
        Assert.DoesNotContain(new SqliteSettingsStore($"Data Source={factory.DatabasePath}").LoadAll().Keys, k => k == SettingKeys.ForecastLatitude);
    }

    [Fact]
    public async Task PutSettings_NoPin_SavesAndGetReflectsValue()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        using var put = await client.PutAsJsonAsync(settingsUri, new Dictionary<string, string?>
        {
            [SettingKeys.ForecastLatitude] = "39.36",
            [SettingKeys.ImmichApiKey] = "secret-key",
            ["Unknown:Key"] = "x",
        });
        put.EnsureSuccessStatusCode();

        var fields = await client.GetFromJsonAsync<List<FieldDto>>(settingsUri);
        Assert.NotNull(fields);
        Assert.Equal("39.36", fields.Single(f => f.Key == SettingKeys.ForecastLatitude).Value);
        var secret = fields.Single(f => f.Key == SettingKeys.ImmichApiKey);
        Assert.True(secret.IsSecret);
        Assert.Equal(SettingsForm.MaskedValue, secret.Value);

        var stored = new SqliteSettingsStore($"Data Source={factory.DatabasePath}").LoadAll();
        Assert.Equal("secret-key", stored[SettingKeys.ImmichApiKey]);
        Assert.False(stored.ContainsKey("Unknown:Key"));
    }

    [Fact]
    public async Task PutSettings_InvalidNumber_Returns400AndSavesNothing()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        using var put = await client.PutAsJsonAsync(settingsUri, new Dictionary<string, string?>
        {
            [SettingKeys.SlideSeconds] = "fast",
            [SettingKeys.NightscoutUrl] = "https://ns.example",
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Empty(new SqliteSettingsStore($"Data Source={factory.DatabasePath}").LoadAll());
    }

    [Fact]
    public async Task Restart_Returns202AndRequestsRestart()
    {
        var restarter = new FakeRestarter();
        using var factory = new TestServerFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IAppRestarter>(restarter)));
        using var client = configured.CreateClient();

        using var response = await client.PostAsync(new Uri("/api/settings/restart", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        for (var i = 0; i < 50 && restarter.Calls == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, restarter.Calls);
    }

    [Fact]
    public async Task Restart_CrossSiteRequest_IsForbidden()
    {
        var restarter = new FakeRestarter();
        using var factory = new TestServerFactory();
        using var configured = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IAppRestarter>(restarter)));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/settings/restart", UriKind.Relative));
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, restarter.Calls);
    }
}
