namespace HomeSpeaker.Server2.Telemetry;

/// <summary>How many warnings or errors a category has logged since startup, split by exception type.</summary>
public sealed record LogEventCount(string Level, string Category, string? Exception, long Count);
