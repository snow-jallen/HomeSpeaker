using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared.Anchors;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class DashboardAnchorToggleTests
{
    [Fact]
    public async Task SetAnchorDoneAsync_WhenTheSaveFails_ReturnsUnavailableInsteadOfThrowing()
    {
        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Features:Anchors"] = "true" });
        using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IAnchorStore, FailingAnchorStore>()));
        await using var scope = app.Services.CreateAsyncScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<DashboardState>();
        var walk = new AnchorDto("1", "Walk", "day", 0, null, DayMask.Every, false, [], "local", true, true, 0, []);

        var result = await dashboard.SetAnchorDoneAsync(walk, done: true);

        Assert.Equal(AnchorResult.Unavailable, result);
    }
}
