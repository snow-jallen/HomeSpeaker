using System.Net;
using System.Net.Http.Json;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorApiTests : IDisposable
{
    private readonly TestServerFactory factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Features:Anchors"] = "true" });
    private readonly HttpClient client;

    public AnchorApiTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private static Uri uri(string path) => new(path, UriKind.Relative);

    private async Task<AnchorBoardDto> boardAsync(string query = "")
    {
        var board = await client.GetFromJsonAsync<AnchorBoardDto>(uri("/api/anchors/board" + query));
        return board!;
    }

    private async Task<(string PersonId, string AnchorId, DateOnly Today)> personWithAnchorAsync()
    {
        var created = await client.PostAsJsonAsync(uri("/api/anchors/people"), new AnchorPersonRequest("Sam"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var person = (await created.Content.ReadFromJsonAsync<AnchorPersonDto>())!;

        var anchorResponse = await client.PostAsJsonAsync(uri($"/api/anchors/people/{person.Id}/anchors"), new AnchorRequest("Walk", "morning"));
        Assert.Equal(HttpStatusCode.Created, anchorResponse.StatusCode);
        var anchor = (await anchorResponse.Content.ReadFromJsonAsync<AnchorDto>())!;
        return (person.Id, anchor.Id, (await boardAsync()).Today);
    }

    [Fact]
    public async Task CreatePersonAnchorTickAndReadBoard_RoundTrip()
    {
        var (personId, anchorId, today) = await personWithAnchorAsync();

        var tick = await client.PutAsJsonAsync(uri($"/api/anchors/anchors/{anchorId}/days/{today:yyyy-MM-dd}"), new AnchorLogRequest(true));
        Assert.Equal(HttpStatusCode.NoContent, tick.StatusCode);

        var person = Assert.Single((await boardAsync()).People);
        Assert.Equal((personId, "Sam", 1, 1), (person.Id, person.Name, person.TodayDone, person.TodayTotal));
        Assert.Equal("kept", person.Anchors.Single().Cells.Single().State);

        var day = await client.GetFromJsonAsync<AnchorPersonDto>(uri($"/api/anchors/people/{personId}/day?date={today:yyyy-MM-dd}"));
        Assert.Equal("Walk", day!.Anchors.Single().Title);
    }

    [Fact]
    public async Task Board_SerializesStatesAndSlotsAsLowercaseStrings()
    {
        await personWithAnchorAsync();

        var json = await client.GetStringAsync(uri("/api/anchors/board"));

        Assert.Contains("\"slot\":\"morning\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"open\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PutLog_IsIdempotent()
    {
        var (_, anchorId, today) = await personWithAnchorAsync();
        var request = new AnchorLogRequest(true, DateTimeOffset.UtcNow);

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PutAsJsonAsync(uri($"/api/anchors/anchors/{anchorId}/days/{today:yyyy-MM-dd}"), request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(1, (await boardAsync()).People.Single().TodayDone);
    }

    [Fact]
    public async Task PutLog_OnDayOutsidePeriods_IsKeptBonus()
    {
        var (_, anchorId, today) = await personWithAnchorAsync();
        var lastWeek = today.AddDays(-7);

        var response = await client.PutAsJsonAsync(uri($"/api/anchors/anchors/{anchorId}/days/{lastWeek:yyyy-MM-dd}"), new AnchorLogRequest(true));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cells = (await boardAsync($"?from={lastWeek:yyyy-MM-dd}&to={today:yyyy-MM-dd}")).People.Single().Anchors.Single().Cells;
        Assert.Equal("kept", cells.First().State);
        Assert.Equal("none", cells[1].State);
    }

    [Fact]
    public async Task PutLog_TwoDaysAhead_400()
    {
        var (_, anchorId, today) = await personWithAnchorAsync();

        var response = await client.PutAsJsonAsync(uri($"/api/anchors/anchors/{anchorId}/days/{today.AddDays(2):yyyy-MM-dd}"), new AnchorLogRequest(true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownAnchorOrPerson_404()
    {
        var (_, _, today) = await personWithAnchorAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(uri($"/api/anchors/anchors/999/days/{today:yyyy-MM-dd}"), new AnchorLogRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(uri("/api/anchors/people/999/day"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(uri("/api/anchors/anchors/999/retire"), null)).StatusCode);
    }

    [Fact]
    public async Task InvalidInput_400WithMessage()
    {
        var response = await client.PostAsJsonAsync(uri("/api/anchors/people"), new AnchorPersonRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditRetireRestoreOrderArchive_Work()
    {
        var (personId, anchorId, _) = await personWithAnchorAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(uri($"/api/anchors/anchors/{anchorId}"), new AnchorRequest("Long walk", Days: 62))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(uri($"/api/anchors/anchors/{anchorId}/retire"), null)).StatusCode);
        Assert.True((await boardAsync()).People.Single().Anchors.Single().Retired);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(uri($"/api/anchors/anchors/{anchorId}/restore"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(uri($"/api/anchors/people/{personId}/anchors/order"), new AnchorOrderRequest([anchorId]))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(uri($"/api/anchors/people/{personId}"), new AnchorPersonRequest("Samuel", "rose"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(uri("/api/anchors/people/order"), new AnchorOrderRequest([personId]))).StatusCode);

        var person = (await boardAsync()).People.Single();
        Assert.Equal(("Samuel", "rose", "Long walk", false), (person.Name, person.Color, person.Anchors.Single().Title, person.Anchors.Single().Retired));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(uri($"/api/anchors/people/{personId}"))).StatusCode);
        Assert.Empty((await boardAsync()).People);
    }

    [Fact]
    public async Task PeopleList_ReturnsTodaysPeople()
    {
        await personWithAnchorAsync();

        var people = await client.GetFromJsonAsync<List<AnchorPersonDto>>(uri("/api/anchors/people"));

        Assert.Equal("Sam", Assert.Single(people!).Name);
    }

    [Fact]
    public async Task Board_RangeOver92Days_Clamped()
    {
        var today = (await boardAsync()).Today;

        var board = await boardAsync($"?from={today.AddDays(-200):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.Equal(today.AddDays(-91), board.From);
    }
}
