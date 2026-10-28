namespace HomeSpeaker.Server2;

/// <summary>
/// One full rescan of the media folder. <see cref="DiskReadBytes"/> is the whole process's disk reads over the rescan
/// (from <c>/proc/self/io</c>; null off Linux), so it includes anything else reading at the same time.
/// </summary>
public sealed record LibrarySyncRecord(
    DateTimeOffset StartedUtc,
    TimeSpan Duration,
    string Reason,
    int Files,
    int Failures,
    long? DiskReadBytes);
