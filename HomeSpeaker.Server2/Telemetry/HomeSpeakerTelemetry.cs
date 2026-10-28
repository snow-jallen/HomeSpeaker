using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// The app's own trace source and meter. Both use the application name, which <see cref="Extensions.ConfigureOpenTelemetry"/>
/// registers with the tracing and metrics pipelines, so anything recorded here flows to the OTLP exporter when one is configured.
/// </summary>
public static class HomeSpeakerTelemetry
{
    public const string Name = "HomeSpeaker.Server2";

    public static readonly ActivitySource ActivitySource = new(Name);

    public static readonly Meter Meter = new(Name);
}
