using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// A logger provider that writes nothing; it counts warnings and errors by category and exception type, and keeps the
/// most recent ones, so a flood like 260 identical warnings in two minutes shows up as a number instead of log noise.
/// </summary>
public sealed class LogEventCounter : ILoggerProvider
{
    private const int RecentSize = 50;
    private const int MaxMessageLength = 300;

    private static readonly Counter<long> eventCounter = HomeSpeakerTelemetry.Meter.CreateCounter<long>(
        "homespeaker.log.events", description: "Warnings and errors logged, by level and category");

    private readonly ConcurrentDictionary<(LogLevel Level, string Category, string? Exception), long> counts = new();
    private readonly ConcurrentQueue<LogEventRecord> recent = new();
    private readonly TimeProvider time;

    public LogEventCounter(TimeProvider time)
    {
        this.time = time;
    }

    public long Total => counts.Values.Sum();

    public IReadOnlyList<LogEventCount> Counts =>
        counts.Select(pair => new LogEventCount(pair.Key.Level.ToString(), pair.Key.Category, pair.Key.Exception, pair.Value))
            .OrderByDescending(c => c.Count)
            .ToList();

    /// <summary>The latest warnings and errors, oldest first.</summary>
    public IReadOnlyList<LogEventRecord> Recent => recent.ToArray();

    public ILogger CreateLogger(string categoryName) => new CountingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void record(LogLevel level, string category, Exception? exception, string message)
    {
        var exceptionType = exception?.GetType().Name;
        counts.AddOrUpdate((level, category, exceptionType), 1, (_, count) => count + 1);
        eventCounter.Add(1,
            new KeyValuePair<string, object?>("level", level.ToString()),
            new KeyValuePair<string, object?>("category", category));

        if (message.Length > MaxMessageLength)
        {
            message = message[..MaxMessageLength] + "…";
        }

        recent.Enqueue(new LogEventRecord(time.GetUtcNow(), level.ToString(), category, exceptionType, message));
        while (recent.Count > RecentSize)
        {
            recent.TryDequeue(out _);
        }
    }

    private sealed class CountingLogger(LogEventCounter owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            owner.record(logLevel, category, exception, formatter(state, exception));
        }
    }
}
