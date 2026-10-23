using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Tests.Photos;

namespace HomeSpeaker.Server2.Tests;

public sealed class LocationSearchServiceTests
{
    private static (LocationSearchService Service, FakeHttpMessageHandler Handler) create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://geocoding-api.open-meteo.com/") };
        return (new LocationSearchService(http), handler);
    }

    [Fact]
    public async Task Search_ParsesOpenMeteoResults()
    {
        var (service, handler) = create((_, _) => FakeHttpMessageHandler.Json("""
            {"results":[
              {"id":5774001,"name":"Ephraim","latitude":39.35969,"longitude":-111.58631,"elevation":1680.0,"feature_code":"PPL","country_code":"US","admin1_id":5549030,"timezone":"America/Denver","population":7135,"country":"United States","admin1":"Utah","admin2":"Sanpete"},
              {"id":293100,"name":"Ephraim","latitude":31.5,"longitude":34.5,"country_code":"IL","timezone":"Asia/Jerusalem"}
            ],"generationtime_ms":0.6}
            """));

        var matches = await service.SearchAsync("Ephraim", CancellationToken.None);

        Assert.Equal(
            [
                new LocationMatch("Ephraim", "Utah", "United States", 39.35969, -111.58631),
                new LocationMatch("Ephraim", null, null, 31.5, 34.5),
            ],
            matches);
        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal("https://geocoding-api.open-meteo.com/v1/search?name=Ephraim&count=5", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Search_EscapesQuery_AndNoResultsIsEmpty()
    {
        var (service, handler) = create((_, _) => FakeHttpMessageHandler.Json("""{"generationtime_ms":0.2}"""));

        var matches = await service.SearchAsync(" Salt Lake & City ", CancellationToken.None);

        Assert.Empty(matches);
        Assert.Equal("https://geocoding-api.open-meteo.com/v1/search?name=Salt%20Lake%20%26%20City&count=5", handler.Requests[0].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Search_BlankQuery_ReturnsEmptyWithoutHttpCall()
    {
        var (service, handler) = create((_, _) => throw new InvalidOperationException("no HTTP expected"));

        var matches = await service.SearchAsync("   ", CancellationToken.None);

        Assert.Empty(matches);
        Assert.Empty(handler.Requests);
    }
}
