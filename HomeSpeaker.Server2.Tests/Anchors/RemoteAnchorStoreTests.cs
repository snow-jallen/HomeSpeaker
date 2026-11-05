using System.Net;
using System.Net.Http.Json;
using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Anchors;

/// <summary>A HomeSpeaker whose anchor source is another HomeSpeaker (the "home"), here a real test server.</summary>
public sealed class RemoteAnchorStoreTests : IDisposable
{
    private readonly TestServerFactory home = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Features:Anchors"] = "true" });
    private readonly SwitchableClients clients;
    private readonly AnchorChangeNotifier notifier = new();
    private readonly RemoteAnchorCache cache = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);

    public RemoteAnchorStoreTests()
    {
        clients = new SwitchableClients(home);
    }

    public void Dispose() => home.Dispose();

    private RemoteAnchorStore store() =>
        new(clients, config(), notifier, cache, clock, NullLogger<RemoteAnchorStore>.Instance);

    private static IConfiguration config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Anchors:Source"] = "remote", ["Anchors:RemoteUrl"] = "http://localhost/" })
        .Build();

    [Fact]
    public async Task Remote_ReadsAndWritesThroughHome()
    {
        var remote = store();
        var person = (await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"))).Value!;
        var anchor = (await remote.CreateAnchorAsync(person.Id, new AnchorRequest("Walk", "morning"))).Value!;
        var today = (await remote.GetBoardAsync()).Today;

        Assert.Equal(AnchorResult.Ok, await remote.SetDoneAsync(anchor.Id, today, new AnchorLogRequest(true)));

        using var homeClient = home.CreateClient();
        var homeBoard = await homeClient.GetFromJsonAsync<AnchorBoardDto>(new Uri("/api/anchors/board", UriKind.Relative));
        Assert.Equal("kept", homeBoard!.People.Single().Anchors.Single().Cells.Single().State);
        Assert.Equal("kept", (await remote.GetDayAsync(person.Id))!.Anchors.Single().Cells.Single().State);
    }

    [Fact]
    public async Task Remote_WriteRaisesChanged()
    {
        var remote = store();
        var raised = 0;
        notifier.Changed += () => raised++;

        await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"));

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Remote_HomeDown_ShowsStaleBoard()
    {
        var remote = store();
        await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"));
        await remote.GetBoardAsync();

        clients.Down = true;
        clock.Advance(TimeSpan.FromMinutes(1));
        var board = await remote.GetBoardAsync();

        Assert.True(board.Stale);
        Assert.Equal("Sam", board.People.Single().Name);
    }

    [Fact]
    public async Task Remote_HomeDown_WaitsBeforeAskingAgain()
    {
        var remote = store();
        await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"));
        await remote.GetBoardAsync();
        clients.Down = true;
        clock.Advance(TimeSpan.FromMinutes(1));

        await remote.GetBoardAsync();
        await remote.GetBoardAsync();
        Assert.Equal(1, clients.FailedCalls);

        clock.Advance(TimeSpan.FromSeconds(31));
        await remote.GetBoardAsync();
        Assert.Equal(2, clients.FailedCalls);
    }

    [Fact]
    public async Task Remote_HomeDown_DayComesFromTheLastBoard()
    {
        var remote = store();
        var sam = (await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"))).Value!;
        await remote.GetBoardAsync();
        clients.Down = true;

        var day = await remote.GetDayAsync(sam.Id);

        Assert.Equal("Sam", day!.Name);
    }

    [Fact]
    public async Task Remote_HomeDownWithNoBoard_DayThrowsRatherThanSayingGone()
    {
        clients.Down = true;

        await Assert.ThrowsAsync<AnchorSourceUnavailableException>(() => store().GetDayAsync("1"));
    }

    [Fact]
    public async Task Remote_NoUrl_WritesReportUnavailable()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Anchors:Source"] = "remote" }).Build();
        var remote = new RemoteAnchorStore(clients, empty, notifier, cache, clock, NullLogger<RemoteAnchorStore>.Instance);

        Assert.Equal(AnchorResult.Unavailable, (await remote.CreatePersonAsync(new AnchorPersonRequest("Sam"))).Result);
    }

    [Fact]
    public async Task Remote_HomeDownWithNothingCached_Throws()
    {
        clients.Down = true;

        await Assert.ThrowsAsync<AnchorSourceUnavailableException>(() => store().GetBoardAsync());
    }

    [Fact]
    public async Task Remote_WriteFails_ReturnsUnavailable()
    {
        clients.Down = true;

        Assert.Equal(AnchorResult.Unavailable, await store().SetDoneAsync("1", new DateOnly(2026, 11, 4), new AnchorLogRequest(true)));
    }

    [Fact]
    public async Task Remote_ErrorsMapToResults()
    {
        var remote = store();

        var invalid = await remote.CreatePersonAsync(new AnchorPersonRequest(""));
        Assert.Equal(AnchorResult.Invalid, invalid.Result);
        Assert.Contains("name", invalid.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AnchorResult.NotFound, await remote.RetireAnchorAsync("999"));
        Assert.Null(await remote.GetDayAsync("999"));
    }

    [Fact]
    public async Task Remote_NoUrl_Throws()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Anchors:Source"] = "remote" }).Build();
        var remote = new RemoteAnchorStore(clients, empty, notifier, cache, clock, NullLogger<RemoteAnchorStore>.Instance);

        await Assert.ThrowsAsync<AnchorSourceUnavailableException>(() => remote.GetBoardAsync());
    }

    [Fact]
    public async Task App_WithRemoteSourceUnreachable_BoardIs503()
    {
        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?>
        {
            ["Features:Anchors"] = "true",
            ["Anchors:Source"] = "remote",
            ["Anchors:RemoteUrl"] = "http://127.0.0.1:9/",
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/anchors/board", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>Hands out clients that talk to the home test server, or fail as if it were unplugged.</summary>
    private sealed class SwitchableClients(TestServerFactory home) : IHttpClientFactory
    {
        public bool Down { get; set; }

        public int FailedCalls { get; private set; }

        public HttpClient CreateClient(string name) => Down ? new HttpClient(new FailingHandler(this)) : home.CreateClient();

        private sealed class FailingHandler(SwitchableClients owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.FailedCalls++;
                throw new HttpRequestException("Connection refused");
            }
        }
    }
}
