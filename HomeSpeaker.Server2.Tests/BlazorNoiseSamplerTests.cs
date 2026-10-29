using System.Diagnostics;
using HomeSpeaker.Server2.Telemetry;
using OpenTelemetry.Trace;

namespace HomeSpeaker.Server2.Tests;

public class BlazorNoiseSamplerTests
{
    private static SamplingDecision decide(string name, ActivityKind kind = ActivityKind.Server) =>
        new BlazorNoiseSampler(new AlwaysOnSampler())
            .ShouldSample(new SamplingParameters(default, ActivityTraceId.CreateRandom(), name, kind))
            .Decision;

    [Theory]
    [InlineData("Microsoft.AspNetCore.Components.Server.ComponentHub/OnRenderCompleted")]
    [InlineData("Microsoft.AspNetCore.Components.Server.ComponentHub/EndInvokeJSFromDotNet")]
    public void Drops_render_and_interop_acknowledgements(string name) =>
        Assert.Equal(SamplingDecision.Drop, decide(name));

    [Theory]
    [InlineData("Microsoft.AspNetCore.Components.Server.ComponentHub/BeginInvokeDotNetFromJS")]
    [InlineData("Microsoft.AspNetCore.Components.Server.ComponentHub/ConnectCircuit")]
    [InlineData("Event onclick -> HomeSpeaker.Server2.Components.Layout.MainLayout.ShowPhotosAsync")]
    [InlineData("GET /api/photos/next")]
    [InlineData("OnRenderCompleted")]
    public void Keeps_everything_else(string name) =>
        Assert.Equal(SamplingDecision.RecordAndSample, decide(name));
}
