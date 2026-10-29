using OpenTelemetry.Trace;

namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// Drops the SignalR spans Blazor Server records for its own plumbing: every render batch the browser
/// acknowledges (<c>OnRenderCompleted</c>) and every JS interop reply (<c>EndInvokeJSFromDotNet</c>). An open
/// photo slideshow produces several a second, which made them over 95% of all spans while saying nothing about
/// what anyone did. Circuit connects and UI events (<c>BeginInvokeDotNetFromJS</c>, <c>Event onclick ...</c>) are kept.
/// Everything else goes to <paramref name="inner"/>.
/// </summary>
public sealed class BlazorNoiseSampler(Sampler inner) : Sampler
{
    private const string ComponentHub = "Microsoft.AspNetCore.Components.Server.ComponentHub/";

    private static readonly SamplingResult drop = new(SamplingDecision.Drop);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var name = samplingParameters.Name;
        if (name.StartsWith(ComponentHub, StringComparison.Ordinal)
            && (name.EndsWith("/OnRenderCompleted", StringComparison.Ordinal)
                || name.EndsWith("/EndInvokeJSFromDotNet", StringComparison.Ordinal)))
        {
            return drop;
        }

        return inner.ShouldSample(samplingParameters);
    }
}
