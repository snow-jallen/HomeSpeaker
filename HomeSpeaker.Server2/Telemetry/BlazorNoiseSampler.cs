using OpenTelemetry.Trace;

namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// Drops the SignalR spans Blazor Server records for its own plumbing: every render batch the browser
/// acknowledges (<c>OnRenderCompleted</c>) and every JS interop reply (<c>EndInvokeJSFromDotNet</c>). An open
/// photo slideshow produces several a second, which made them over 95% of all spans while saying nothing about
/// what anyone did. Circuit connects and UI events (<c>BeginInvokeDotNetFromJS</c>, <c>Event onclick ...</c>) are kept.
/// Everything else goes to <paramref name="inner"/>.
/// <para>
/// SignalR names every hub call <c>Microsoft.AspNetCore.SignalR.Server.InvocationIn</c> when it creates the span
/// and only sets the <c>ComponentHub/OnRenderCompleted</c> display name afterwards, so this matches on the
/// <c>rpc.service</c> and <c>rpc.method</c> tags, which it passes at creation.
/// </para>
/// </summary>
public sealed class BlazorNoiseSampler(Sampler inner) : Sampler
{
    public const string ComponentHub = "Microsoft.AspNetCore.Components.Server.ComponentHub";

    private static readonly SamplingResult drop = new(SamplingDecision.Drop);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        string? service = null, method = null;
        if (samplingParameters.Tags is { } tags)
        {
            foreach (var tag in tags)
            {
                switch (tag.Key)
                {
                    case "rpc.service":
                        service = tag.Value as string;
                        break;
                    case "rpc.method":
                        method = tag.Value as string;
                        break;
                }
            }
        }

        if (service == ComponentHub && method is "OnRenderCompleted" or "EndInvokeJSFromDotNet")
        {
            return drop;
        }

        return inner.ShouldSample(samplingParameters);
    }
}
