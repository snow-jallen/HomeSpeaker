using System.Net;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class BloodSugarServiceTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static string entry(int minutesAgo, int sgv, string type = "sgv", string direction = "Flat") =>
        $"{{\"sgv\":{sgv},\"dateString\":\"{now.AddMinutes(-minutesAgo).UtcDateTime:yyyy-MM-ddTHH:mm:ss.000Z}\",\"direction\":\"{direction}\",\"type\":\"{type}\"}}";

    private static (BloodSugarService Service, FakeHttpMessageHandler Handler) create(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["NIGHTSCOUT_URL"] = "https://ns.test/" })
            .Build();
        var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json(json, status));
        var service = new BloodSugarService(new HttpClient(handler), NullLogger<BloodSugarService>.Instance, configuration, new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(now));
        return (service, handler);
    }

    [Fact]
    public async Task ParsesCurrentReadingAndRecentHistory_NewestFirst()
    {
        var (service, _) = create($"[{entry(10, 110)},{entry(5, 118)},{entry(0, 125, direction: "SingleUp")}]");

        var status = await service.GetBloodSugarStatusAsync();

        Assert.Equal(125, status.CurrentReading!.Sgv);
        Assert.Equal("SingleUp", status.CurrentReading.Direction);
        Assert.Equal([125, 118, 110], status.RecentReadings.Select(r => r.Sgv));
        Assert.Equal(7, status.Delta);
        Assert.False(status.IsStale);
    }

    [Fact]
    public async Task HistoryOnlyKeepsTheLastThreeHours_ButCurrentReadingStillSet()
    {
        var (service, _) = create($"[{entry(1, 120)},{entry(170, 100)},{entry(190, 90)},{entry(400, 80)}]");

        var status = await service.GetBloodSugarStatusAsync();

        Assert.Equal(120, status.CurrentReading!.Sgv);
        Assert.Equal([120, 100], status.RecentReadings.Select(r => r.Sgv));
    }

    [Fact]
    public async Task IgnoresNonSgvEntriesAndZeroReadings()
    {
        var (service, _) = create($"[{entry(2, 0)},{entry(3, 250, type: "mbg")},{entry(4, 130)}]");

        var status = await service.GetBloodSugarStatusAsync();

        Assert.Equal(130, status.CurrentReading!.Sgv);
        Assert.Single(status.RecentReadings);
    }

    [Fact]
    public async Task StaleCurrentReading_IsFlagged()
    {
        var (service, _) = create($"[{entry(42, 140)}]");

        var status = await service.GetBloodSugarStatusAsync();

        Assert.True(status.IsStale);
        Assert.Equal(140, status.CurrentReading!.Sgv);
    }

    [Fact]
    public async Task OneRequestServesBothCurrentAndHistory_AndIsCached()
    {
        var (service, handler) = create($"[{entry(0, 125)},{entry(5, 120)}]");

        await service.GetBloodSugarStatusAsync();
        await service.GetBloodSugarStatusAsync();

        var request = Assert.Single(handler.Requests).Request;
        Assert.Contains("count=40", request.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyResponse_GivesNoReading()
    {
        var (service, _) = create("[]");

        var status = await service.GetBloodSugarStatusAsync();

        Assert.Null(status.CurrentReading);
        Assert.Empty(status.RecentReadings);
        Assert.True(status.IsStale);
    }

    [Fact]
    public async Task ServerError_GivesNoReading()
    {
        var (service, _) = create("{}", HttpStatusCode.InternalServerError);

        var status = await service.GetBloodSugarStatusAsync();

        Assert.Null(status.CurrentReading);
    }
}
