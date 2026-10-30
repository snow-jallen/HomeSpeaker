using System.Diagnostics;
using System.Diagnostics.Metrics;
using HomeSpeaker.Server2.Telemetry;

namespace HomeSpeaker.Server2;

public sealed class Mp3Library : IDisposable
{
    private const int SyncHistorySize = 20;

    private static readonly Counter<long> syncCounter = HomeSpeakerTelemetry.Meter.CreateCounter<long>(
        "homespeaker.library.syncs", description: "Full rescans of the media folder, tagged by what asked for them");
    private static readonly Histogram<double> syncDuration = HomeSpeakerTelemetry.Meter.CreateHistogram<double>(
        "homespeaker.library.sync.duration", unit: "s", description: "How long a full rescan took");
    private static readonly Counter<long> parseFailureCounter = HomeSpeakerTelemetry.Meter.CreateCounter<long>(
        "homespeaker.library.parse_failures", description: "Files whose tags could not be read during a rescan");
    private static readonly Counter<long> staleReadCounter = HomeSpeakerTelemetry.Meter.CreateCounter<long>(
        "homespeaker.library.stale_reads", description: "Reads served the previous song list because a rescan was already running");
    private static readonly Counter<long> fileSystemEventCounter = HomeSpeakerTelemetry.Meter.CreateCounter<long>(
        "homespeaker.library.filesystem_events", description: "MP3 create/delete/rename events from the watcher");

    private readonly IFileSource fileSource;
    private readonly ITagParser tagParser;
    private readonly IDataStore dataStore;
    private readonly ILogger<Mp3Library> logger;
    private readonly object syncLock = new();
    private readonly object historyLock = new();
    private readonly Queue<LibrarySyncRecord> syncHistory = new();
    private FileSystemWatcher? watcher;
    private Timer? debounceTimer;
    private int dirty;
    private string? dirtyReason;
    private string? lastFileSystemChange;
    private bool disposed;
    private long generation;

    public event EventHandler? LibraryChanged;

    public Mp3Library(IFileSource fileSource, ITagParser tagParser, IDataStore dataStore, ILogger<Mp3Library> logger)
    {
        this.fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
        this.tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.logger.LogInformation("Initialized with fileSource {RootFolder}", fileSource.RootFolder);

        lock (syncLock)
        {
            syncLibrary("startup");
        }

        startFileSystemWatcher();
    }

    private void startFileSystemWatcher()
    {
        var rootFolder = fileSource.RootFolder.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (!Directory.Exists(rootFolder))
        {
            logger.LogWarning("Media folder {RootFolder} does not exist; filesystem watcher not started.", rootFolder);
            return;
        }

        debounceTimer = new Timer(onDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        watcher = new FileSystemWatcher(rootFolder, "*.mp3")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        watcher.Created += onFileSystemChanged;
        watcher.Deleted += onFileSystemChanged;
        watcher.Renamed += onFileSystemChanged;

        logger.LogInformation("Filesystem watcher started for {RootFolder}", rootFolder);
    }

    private void onFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        logger.LogInformation("Detected filesystem change ({ChangeType}): {Path}", e.ChangeType, e.FullPath);
        fileSystemEventCounter.Add(1, new KeyValuePair<string, object?>("change_type", e.ChangeType.ToString()));
        lastFileSystemChange = $"filesystem {e.ChangeType}: {Path.GetFileName(e.FullPath)}";
        // Debounce: reset the timer so rapid successive changes result in a single reload
        debounceTimer?.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private void onDebounceElapsed(object? state)
    {
        MarkDirty(lastFileSystemChange ?? "filesystem");
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public string RootFolder => fileSource.RootFolder;

    /// <summary>True when the files changed since the last rescan; the next read of <see cref="Songs"/> rescans.</summary>
    public bool IsDirty => Volatile.Read(ref dirty) == 1;

    /// <summary>Songs in the current list, without triggering a rescan when dirty.</summary>
    public int SongCount => dataStore.GetSongs().Count();

    /// <summary>Goes up by one each time a rescan swaps in a new song list, so caches built from <see cref="Songs"/> know when to rebuild.</summary>
    public long Generation => Interlocked.Read(ref generation);

    /// <summary>True while a rescan is running.</summary>
    public bool IsSyncing { get; private set; }

    /// <summary>The most recent rescans, oldest first.</summary>
    public IReadOnlyList<LibrarySyncRecord> SyncHistory
    {
        get
        {
            lock (historyLock)
            {
                return syncHistory.ToList();
            }
        }
    }

    /// <summary>Flags the library for a rescan on the next read. <paramref name="reason"/> shows up in telemetry.</summary>
    public void MarkDirty(string reason)
    {
        Volatile.Write(ref dirtyReason, reason);
        Volatile.Write(ref dirty, 1);
    }

    public IEnumerable<Shared.Song> Songs
    {
        get
        {
            if (IsDirty)
            {
                refreshIfDirty();
            }

            return dataStore.GetSongs();
        }
    }

    private void refreshIfDirty()
    {
        // A rescan takes ~20 s on a Pi. Only one reader does it; the rest get the current list rather than queueing
        // up behind the lock and each running another full rescan (which kept the SD card busy indefinitely).
        if (!Monitor.TryEnter(syncLock))
        {
            staleReadCounter.Add(1);
            return;
        }

        try
        {
            // Clear the flag before scanning so a change that lands mid-rescan triggers exactly one more.
            if (Interlocked.Exchange(ref dirty, 0) == 1)
            {
                syncLibrary(Interlocked.Exchange(ref dirtyReason, null) ?? "unknown");
            }
        }
        finally
        {
            Monitor.Exit(syncLock);
        }
    }

    // Callers hold syncLock.
    private void syncLibrary(string reason)
    {
        using var activity = HomeSpeakerTelemetry.ActivitySource.StartActivity("library.sync");
        activity?.SetTag("homespeaker.library.sync.reason", reason);
        logger.LogInformation("Synchronizing MP3 library - reloading from disk ({Reason}).", reason);

        IsSyncing = true;
        var startedUtc = DateTimeOffset.UtcNow;
        var readBefore = LinuxProcStats.DiskReadBytes();
        var stopwatch = Stopwatch.StartNew();
        var songs = new List<Shared.Song>();
        var files = 0;
        var failures = 0;
        try
        {
            foreach (var file in fileSource.GetAllMp3s())
            {
                files++;
                try
                {
                    songs.Add(tagParser.CreateSong(file));
                }
                catch (Exception ex)
                {
                    failures++;
                    logger.LogError(ex, "Trouble parsing tag info!");
                }
            }

            dataStore.ReplaceAll(songs);
            Interlocked.Increment(ref generation);
        }
        finally
        {
            IsSyncing = false;
        }

        stopwatch.Stop();
        var readBytes = LinuxProcStats.DiskReadBytes() - readBefore;
        var record = new LibrarySyncRecord(startedUtc, stopwatch.Elapsed, reason, files, failures, readBytes);
        lock (historyLock)
        {
            syncHistory.Enqueue(record);
            while (syncHistory.Count > SyncHistorySize)
            {
                syncHistory.Dequeue();
            }
        }

        var reasonTag = new KeyValuePair<string, object?>("reason", reasonCategory(reason));
        syncCounter.Add(1, reasonTag);
        syncDuration.Record(stopwatch.Elapsed.TotalSeconds, reasonTag);
        if (failures > 0)
        {
            parseFailureCounter.Add(failures);
        }

        activity?.SetTag("homespeaker.library.files", files);
        activity?.SetTag("homespeaker.library.parse_failures", failures);
        activity?.SetTag("homespeaker.library.disk_read_bytes", readBytes);
        logger.LogInformation(
            "Sync Completed! {Count} songs in database. {Files} files, {Failures} unreadable, {Seconds:F1}s, {ReadMB:F1} MB read from disk.",
            songs.Count, files, failures, stopwatch.Elapsed.TotalSeconds, readBytes / 1048576.0);
    }

    // Metric tags need a small fixed set of values; the full reason (with file names) goes to the trace and history.
    private static string reasonCategory(string reason)
    {
        var separator = reason.IndexOfAny([':', ' ']);
        return separator > 0 ? reason[..separator] : reason;
    }

    internal void DeleteSong(int songId)
    {
        var song = Songs.Where(s => s.SongId == songId).FirstOrDefault();
        if (song?.Path == null)
        {
            return;
        }

        logger.LogWarning("Deleting song# {SongId} at {Path}", songId, song.Path);
        fileSource.SoftDelete(song.Path);
        MarkDirty("song-deleted");
    }

    internal void UpdateSong(int songId, string name, string artist, string album)
    {
        logger.LogInformation("Updating song# {SongId} with name: {Name}, artist: {Artist}, album: {Album}", songId, name, artist, album);

        // Find the song to get its file path
        var song = Songs.Where(s => s.SongId == songId).FirstOrDefault();
        if (song?.Path == null)
        {
            logger.LogWarning("Song with ID {SongId} not found", songId);
            return;
        }

        // Update the MP3 file tags
        tagParser.UpdateSongTags(song.Path, name, artist, album);

        // Update the in-memory data store
        dataStore.UpdateSong(songId, name, artist, album);

        logger.LogInformation("Successfully updated song# {SongId} both in file and in memory", songId);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            debounceTimer?.Dispose();
            watcher?.Dispose();
            disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
