using System.Text.Json;
using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests.Settings;

public class DatabaseSettingsAppTests
{
    [Fact]
    public async Task DatabaseSetting_ReachesRunningApp()
    {
        using var factory = new TestServerFactory();
        new SqliteSettingsStore($"Data Source={factory.DatabasePath}")
            .Save(new Dictionary<string, string?> { ["Features:Anchors"] = "true" });

        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/features", UriKind.Relative)));

        Assert.True(json.RootElement.GetProperty("anchorsEnabled").GetBoolean());
    }
}
