using System.Net;
using HomeSpeaker.Server2.Services.Anchors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class HttpAnchorFeedTests
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private const string payload = """
        {
          "people": [ { "id": "u1", "name": "Dad" } ],
          "anchors": [ { "id": "h1", "personId": "u1", "title": "Stretch", "slot": "morning", "order": 0, "targetTime": null,
                         "activeFrom": "2026-11-01", "activeUntil": null, "missingIs": "missed", "writable": true, "futureField": 1 } ],
          "logs": [ { "anchorId": "h1", "date": "2026-11-03", "done": true, "doneAt": null, "actualTime": null } ]
        }
        """;

    private readonly FakeFeedServer server = new();
    private readonly ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private HttpAnchorFeed feed(string? url = "https://feed.example/api/household/") => new(
        server,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Anchors:Feed:Name"] = "Metanoeo",
            ["Anchors:Feed:Url"] = url,
            ["Anchors:Feed:Token"] = "secret-token",
        }).Build(),
        clock,
        NullLogger<HttpAnchorFeed>.Instance);

    [Fact]
    public async Task Get_SendsBearerAndRange_ParsesPayload()
    {
        server.Respond(HttpStatusCode.OK, payload);

        var snapshot = (await feed().GetAsync(today, CancellationToken.None))!;

        var request = server.Requests.Single();
        Assert.Equal("https://feed.example/api/household/anchors?from=2026-08-05&to=2026-11-04", request.Uri);
        Assert.Equal("Bearer secret-token", request.Authorization);
        Assert.Equal(("Metanoeo", "ok"), (snapshot.Status.Name, snapshot.Status.Status));
        var anchor = Assert.Single(snapshot.Payload!.Anchors);
        Assert.Equal(("Stretch", new DateOnly(2026, 11, 1), true), (anchor.Title, anchor.ActiveFrom, anchor.Writable));
        Assert.True(Assert.Single(snapshot.Payload.Logs).Done);
    }

    [Fact]
    public async Task Get_CachesFor60Seconds()
    {
        server.Respond(HttpStatusCode.OK, payload);
        var client = feed();

        await client.GetAsync(today, CancellationToken.None);
        await client.GetAsync(today, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(61));
        await client.GetAsync(today, CancellationToken.None);

        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task Get_FeedDown_ReturnsLastGoodAsStale()
    {
        server.Respond(HttpStatusCode.OK, payload);
        var client = feed();
        await client.GetAsync(today, CancellationToken.None);

        server.Fail();
        clock.Advance(TimeSpan.FromMinutes(5));
        var snapshot = (await client.GetAsync(today, CancellationToken.None))!;

        Assert.Equal("stale", snapshot.Status.Status);
        Assert.NotNull(snapshot.Status.AsOf);
        Assert.Single(snapshot.Payload!.Anchors);
    }

    [Fact]
    public async Task Get_FeedDownNothingCached_Unreachable()
    {
        server.Respond(HttpStatusCode.Unauthorized, "{}");

        var snapshot = (await feed().GetAsync(today, CancellationToken.None))!;

        Assert.Equal("unreachable", snapshot.Status.Status);
        Assert.Null(snapshot.Payload);
        Assert.NotNull(snapshot.Status.Error);
    }

    [Fact]
    public async Task NotConfigured_ReturnsNull()
    {
        var client = feed(url: null);

        Assert.False(client.IsConfigured);
        Assert.Null(await client.GetAsync(today, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, FeedWriteOutcome.Sent)]
    [InlineData(HttpStatusCode.OK, FeedWriteOutcome.Sent)]
    [InlineData(HttpStatusCode.NotFound, FeedWriteOutcome.Rejected)]
    [InlineData(HttpStatusCode.Conflict, FeedWriteOutcome.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, FeedWriteOutcome.Failed)]
    [InlineData(HttpStatusCode.Unauthorized, FeedWriteOutcome.Failed)]
    public async Task Write_MapsStatus(HttpStatusCode status, FeedWriteOutcome expected)
    {
        server.Respond(status, string.Empty);

        Assert.Equal(expected, await feed().WriteAsync("h1", today, true, clock.GetUtcNow(), CancellationToken.None));
    }

    [Fact]
    public async Task Write_SendsPutWithDoneAndClientTime()
    {
        server.Respond(HttpStatusCode.NoContent, string.Empty);

        await feed().WriteAsync("h 1", today, false, new DateTimeOffset(2026, 11, 4, 13, 2, 11, TimeSpan.Zero), CancellationToken.None);

        var request = server.Requests.Single();
        Assert.Equal(("PUT", "https://feed.example/api/household/anchors/h%201/days/2026-11-04"), (request.Method, request.Uri));
        Assert.Contains("\"done\":false", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"clientUpdatedAt\":\"2026-11-04T13:02:11+00:00\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_NetworkFailure_Failed()
    {
        server.Fail();

        Assert.Equal(FeedWriteOutcome.Failed, await feed().WriteAsync("h1", today, true, clock.GetUtcNow(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_AfterFailure_WaitsBeforeAskingAgain()
    {
        server.Fail();
        var client = feed();

        await client.GetAsync(today, CancellationToken.None);
        var second = (await client.GetAsync(today, CancellationToken.None))!;

        Assert.Equal(1, server.Attempts);
        Assert.Equal("unreachable", second.Status.Status);

        clock.Advance(TimeSpan.FromSeconds(31));
        await client.GetAsync(today, CancellationToken.None);
        Assert.Equal(2, server.Attempts);
    }

    [Fact]
    public async Task Get_ConcurrentCalls_AskTheFeedOnce()
    {
        server.Respond(HttpStatusCode.OK, payload);
        var client = feed();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync(today, CancellationToken.None)));

        Assert.Equal(1, server.Attempts);
    }

    [Fact]
    public async Task Get_MissingWritableMeansWritable_MissingActiveFromIsSkipped()
    {
        server.Respond(HttpStatusCode.OK, """
            {
              "people": [ { "id": "u1", "name": "Dad" } ],
              "anchors": [
                { "id": "h1", "personId": "u1", "title": "Stretch", "activeFrom": "2026-11-01" },
                { "id": "h2", "personId": "u1", "title": "No start date" }
              ]
            }
            """);

        var snapshot = (await feed().GetAsync(today, CancellationToken.None))!;

        var anchor = Assert.Single(snapshot.Payload!.Anchors);
        Assert.Equal(("h1", true), (anchor.Id, anchor.Writable));
        Assert.Empty(snapshot.Payload.Logs);
    }
}
