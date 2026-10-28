namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// One reading from <see cref="ResourceSampler"/>. Rates and deltas cover the interval since the previous sample.
/// CPU is a percentage of one core, like <c>docker stats</c> (so a busy Pi 4 can read up to 400%).
/// Linux-only fields are null elsewhere.
/// </summary>
public sealed record ResourceSample(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    double WorkingSetMB,
    double GcHeapMB,
    long ThreadPoolQueueLength,
    double? DiskReadKBps,
    double? DiskWriteKBps,
    double? ReadCallKBps,
    double? ContainerMemoryMB,
    double? ContainerMemoryLimitMB,
    double? ContainerFileCacheMB,
    long? ContainerLimitHits,
    long? ContainerFileRefaults,
    long LogWarningsAndErrors,
    bool LibrarySyncing);
