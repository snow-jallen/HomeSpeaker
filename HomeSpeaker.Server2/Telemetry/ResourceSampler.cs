using System.Diagnostics;

namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// Samples this process and its container every <see cref="Interval"/> and keeps the last hour, so
/// <c>/api/diagnostics</c> can show what the server was doing when the Pi felt slow, even with no OTLP backend.
/// The same counters are published as OpenTelemetry gauges for when one is configured.
/// </summary>
public sealed class ResourceSampler : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private const int HistorySize = 240;
    private const double Mega = 1024 * 1024;

    private readonly Mp3Library library;
    private readonly LogEventCounter logEvents;
    private readonly TimeProvider time;
    private readonly ILogger<ResourceSampler> logger;
    private readonly object historyLock = new();
    private readonly Queue<ResourceSample> history = new();
    private Reading? previous;

    public ResourceSampler(Mp3Library library, LogEventCounter logEvents, TimeProvider time, ILogger<ResourceSampler> logger)
    {
        this.library = library;
        this.logEvents = logEvents;
        this.time = time;
        this.logger = logger;

        var meter = HomeSpeakerTelemetry.Meter;
        meter.CreateObservableCounter("homespeaker.process.disk.read", () => LinuxProcStats.DiskReadBytes() ?? 0, "By",
            "Bytes this process caused to be read from storage");
        meter.CreateObservableCounter("homespeaker.process.disk.write", () => LinuxProcStats.DiskWriteBytes() ?? 0, "By",
            "Bytes this process caused to be written to storage");
        meter.CreateObservableGauge("homespeaker.container.memory.usage", () => LinuxProcStats.CgroupMemoryCurrent() ?? 0, "By",
            "Memory charged to the container, page cache included");
        meter.CreateObservableGauge("homespeaker.container.memory.file_cache", () => LinuxProcStats.CgroupFileCacheBytes() ?? 0, "By",
            "Page cache charged to the container");
        meter.CreateObservableCounter("homespeaker.container.memory.limit_hits", () => LinuxProcStats.CgroupMemoryLimitHits() ?? 0,
            description: "Times the container hit its memory limit and the kernel reclaimed");
        meter.CreateObservableCounter("homespeaker.container.memory.file_refaults", () => LinuxProcStats.CgroupFileRefaults() ?? 0,
            description: "Evicted file pages the container had to read again");
        meter.CreateObservableGauge("homespeaker.library.songs", () => library.SongCount,
            description: "Songs in the in-memory library");
    }

    /// <summary>The samples from the last hour, oldest first.</summary>
    public IReadOnlyList<ResourceSample> History
    {
        get
        {
            lock (historyLock)
            {
                return history.ToList();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        previous = Reading.Take(time, logEvents.Total);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var current = Reading.Take(time, logEvents.Total);
                var sample = current.Compare(previous, library.IsSyncing);
                previous = current;
                lock (historyLock)
                {
                    history.Enqueue(sample);
                    while (history.Count > HistorySize)
                    {
                        history.Dequeue();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Resource sample failed");
            }
        }
    }

    /// <summary>Raw counters at one instant; two of them make a <see cref="ResourceSample"/>.</summary>
    private sealed record Reading(
        DateTimeOffset At,
        TimeSpan Cpu,
        long? DiskRead,
        long? DiskWrite,
        long? ReadCalls,
        long? LimitHits,
        long? Refaults,
        long LogEvents)
    {
        public static Reading Take(TimeProvider time, long logEvents)
        {
            using var process = Process.GetCurrentProcess();
            return new Reading(
                time.GetUtcNow(),
                process.TotalProcessorTime,
                LinuxProcStats.DiskReadBytes(),
                LinuxProcStats.DiskWriteBytes(),
                LinuxProcStats.ReadCallBytes(),
                LinuxProcStats.CgroupMemoryLimitHits(),
                LinuxProcStats.CgroupFileRefaults(),
                logEvents);
        }

        public ResourceSample Compare(Reading? before, bool librarySyncing)
        {
            using var process = Process.GetCurrentProcess();
            var seconds = before is null ? 0 : (At - before.At).TotalSeconds;
            double? rate(long? now, long? then) =>
                seconds > 0 && now is { } n && then is { } t ? Math.Round((n - t) / 1024.0 / seconds, 1) : null;
            long? delta(long? now, long? then) => now is { } n && then is { } t ? n - t : null;
            var limit = LinuxProcStats.CgroupMemoryMax();

            return new ResourceSample(
                At,
                seconds > 0 ? Math.Round((Cpu - before!.Cpu).TotalSeconds / seconds * 100, 1) : 0,
                Math.Round(process.WorkingSet64 / Mega, 1),
                Math.Round(GC.GetTotalMemory(forceFullCollection: false) / Mega, 1),
                ThreadPool.PendingWorkItemCount,
                rate(DiskRead, before?.DiskRead),
                rate(DiskWrite, before?.DiskWrite),
                rate(ReadCalls, before?.ReadCalls),
                LinuxProcStats.CgroupMemoryCurrent() is { } used ? Math.Round(used / Mega, 1) : null,
                limit is { } max ? Math.Round(max / Mega, 1) : null,
                LinuxProcStats.CgroupFileCacheBytes() is { } cache ? Math.Round(cache / Mega, 1) : null,
                delta(LimitHits, before?.LimitHits),
                delta(Refaults, before?.Refaults),
                before is null ? 0 : LogEvents - before.LogEvents,
                librarySyncing);
        }
    }
}
