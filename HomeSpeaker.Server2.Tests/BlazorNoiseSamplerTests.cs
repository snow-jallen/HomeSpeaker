using System.Diagnostics;
using HomeSpeaker.Server2.Telemetry;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using OpenTelemetry.Trace;

namespace HomeSpeaker.Server2.Tests;

public class BlazorNoiseSamplerTests
{
    private const string invocation = "Microsoft.AspNetCore.SignalR.Server.InvocationIn";

    private static SamplingDecision decide(string name, string? service = null, string? method = null)
    {
        var tags = new List<KeyValuePair<string, object?>> { new("rpc.system", "signalr") };
        if (service is not null) tags.Add(new("rpc.service", service));
        if (method is not null) tags.Add(new("rpc.method", method));
        return new BlazorNoiseSampler(new AlwaysOnSampler())
            .ShouldSample(new SamplingParameters(default, ActivityTraceId.CreateRandom(), name, ActivityKind.Server, tags))
            .Decision;
    }

    [Theory]
    [InlineData("OnRenderCompleted")]
    [InlineData("EndInvokeJSFromDotNet")]
    public void Drops_render_and_interop_acknowledgements(string method) =>
        Assert.Equal(SamplingDecision.Drop, decide(invocation, BlazorNoiseSampler.ComponentHub, method));

    [Theory]
    [InlineData(BlazorNoiseSampler.ComponentHub, "BeginInvokeDotNetFromJS")]
    [InlineData(BlazorNoiseSampler.ComponentHub, "ConnectCircuit")]
    [InlineData("HomeSpeaker.Server2.Hubs.AnchorHub", "OnRenderCompleted")]
    public void Keeps_other_hub_calls(string service, string method) =>
        Assert.Equal(SamplingDecision.RecordAndSample, decide(invocation, service, method));

    [Theory]
    [InlineData("GET /api/photos/next")]
    [InlineData("Event onclick -> HomeSpeaker.Server2.Components.Layout.MainLayout.ShowPhotosAsync")]
    public void Keeps_everything_else(string name) =>
        Assert.Equal(SamplingDecision.RecordAndSample, decide(name));

    /// <summary>
    /// The sampler only works because SignalR hands rpc.service and rpc.method to samplers when it creates a hub
    /// call's span (the name it creates it with is the same for every call). Check that against a real hub.
    /// </summary>
    [Fact]
    public async Task SignalR_passes_hub_and_method_to_samplers()
    {
        var created = new List<ActivityCreationOptions<ActivityContext>>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore.SignalR.Server",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                lock (created) created.Add(options);
                return ActivitySamplingResult.AllDataAndRecorded;
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Features:Anchors"] = "true" });
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "anchorHub"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinAnchorGroup");
        await connection.StopAsync();

        lock (created)
        {
            var call = Assert.Single(created, o => o.Name == invocation);
            var tags = call.Tags!.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("HomeSpeaker.Server2.Hubs.AnchorHub", tags["rpc.service"]);
            Assert.Equal("JoinAnchorGroup", tags["rpc.method"]);
        }
    }
}
