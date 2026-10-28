namespace HomeSpeaker.Server2.Telemetry;

/// <summary>
/// Reads the Linux counters that explain a slow Pi: this process's disk I/O (<c>/proc/self/io</c>) and its container's
/// memory cgroup (<c>/sys/fs/cgroup</c>, cgroup v2). Every method returns null where the file isn't there (Windows, cgroup v1).
/// </summary>
public static class LinuxProcStats
{
    private const string CgroupRoot = "/sys/fs/cgroup";

    /// <summary>Bytes this process has caused to be read from storage (page-cache hits excluded).</summary>
    public static long? DiskReadBytes() => readKeyed("/proc/self/io", "read_bytes");

    /// <summary>Bytes this process has caused to be written to storage.</summary>
    public static long? DiskWriteBytes() => readKeyed("/proc/self/io", "write_bytes");

    /// <summary>Bytes this process has read through read() calls, including page-cache hits.</summary>
    public static long? ReadCallBytes() => readKeyed("/proc/self/io", "rchar");

    /// <summary>Current memory charged to the container, page cache included.</summary>
    public static long? CgroupMemoryCurrent() => readSingle(Path.Combine(CgroupRoot, "memory.current"));

    /// <summary>The container's hard memory limit, or null when unlimited.</summary>
    public static long? CgroupMemoryMax() => readSingle(Path.Combine(CgroupRoot, "memory.max"));

    /// <summary>
    /// How many times the container has hit its memory limit and the kernel had to reclaim (<c>memory.events</c> "max").
    /// A climbing value means page cache is being evicted and re-read.
    /// </summary>
    public static long? CgroupMemoryLimitHits() => readKeyed(Path.Combine(CgroupRoot, "memory.events"), "max");

    public static long? CgroupOomKills() => readKeyed(Path.Combine(CgroupRoot, "memory.events"), "oom_kill");

    /// <summary>Page-cache bytes charged to the container (<c>memory.stat</c> "file").</summary>
    public static long? CgroupFileCacheBytes() => readKeyed(Path.Combine(CgroupRoot, "memory.stat"), "file");

    /// <summary>Times the container re-read a file page it had recently evicted: the cost of a too-small memory limit.</summary>
    public static long? CgroupFileRefaults() => readKeyed(Path.Combine(CgroupRoot, "memory.stat"), "workingset_refault_file");

    private static long? readSingle(string path)
    {
        var text = tryRead(path)?.Trim();
        return long.TryParse(text, out var value) ? value : null;
    }

    private static long? readKeyed(string path, string key)
    {
        var text = tryRead(path);
        if (text is null)
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split([' ', ':'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == key && long.TryParse(parts[1], out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? tryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
