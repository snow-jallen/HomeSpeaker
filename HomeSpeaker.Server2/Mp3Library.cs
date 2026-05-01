namespace HomeSpeaker.Server2;

public sealed class Mp3Library : IDisposable
{
    private readonly IFileSource fileSource;
    private readonly ITagParser tagParser;
    private readonly IDataStore dataStore;
    private readonly ILogger<Mp3Library> logger;
    private readonly object lockObject = new();
    private FileSystemWatcher? watcher;
    private Timer? debounceTimer;
    private bool disposed;

    public event EventHandler? LibraryChanged;

    public Mp3Library(IFileSource fileSource, ITagParser tagParser, IDataStore dataStore, ILogger<Mp3Library> logger)
    {
        this.fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
        this.tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.logger.LogInformation("Initialized with fileSource {RootFolder}", fileSource.RootFolder);

        SyncLibrary();
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
        // Debounce: reset the timer so rapid successive changes result in a single reload
        debounceTimer?.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private void onDebounceElapsed(object? state)
    {
        lock (lockObject)
        {
            IsDirty = true;
        }

        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public string RootFolder => fileSource.RootFolder;

    public void SyncLibrary()
    {
        lock (lockObject)
        {
            logger.LogInformation("Synchronizing MP3 library - reloading from disk.");
            dataStore.Clear();
            var files = fileSource.GetAllMp3s();
            foreach (var file in files)
            {
                try
                {
                    var song = tagParser.CreateSong(file);
                    dataStore.Add(song);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Trouble parsing tag info!");
                }
            }

            logger.LogInformation("Sync Completed! {Count} songs in database.", dataStore.GetSongs().Count());
        }
    }

    public IEnumerable<Shared.Song> Songs
    {
        get
        {
            if (IsDirty)
            {
                ResetLibrary();
            }

            return dataStore.GetSongs();
        }
    }

    public bool IsDirty { get; set; }
    public void ResetLibrary()
    {
        SyncLibrary();
        IsDirty = false;
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
        IsDirty = true;
    }

    internal void UpdateSong(int songId, string name, string artist, string album)
    {
        lock (lockObject)
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