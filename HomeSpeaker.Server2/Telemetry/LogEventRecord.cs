namespace HomeSpeaker.Server2.Telemetry;

public sealed record LogEventRecord(DateTimeOffset TimestampUtc, string Level, string Category, string? Exception, string Message);
