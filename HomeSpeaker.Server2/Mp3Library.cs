using HomeSpeaker.Server2;

namespace HomeSpeaker.Server;

public class Mp3Library
{
    private readonly IFileSource fileSource;
    private readonly ITagParser tagParser;
    private readonly IDataStore dataStore;
    private readonly ILogger<Mp3Library> logger;
    private readonly object lockObject = new();

    public Mp3Library(IFileSource fileSource, ITagParser tagParser, IDataStore dataStore, ILogger<Mp3Library> logger)
    {
        this.fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
        this.tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        logger.LogInformation($"Initialized with fileSource {fileSource.RootFolder}");

        SyncLibrary();
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
            logger.LogInformation("Sync Completed! {count} songs in database.", dataStore.GetSongs().Count());
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

    public bool IsDirty { get; set; } = false;
    public void ResetLibrary()
    {
        SyncLibrary();
        IsDirty = false;
    }

    internal void DeleteSong(int songId)
    {
        var song = Songs.Where(s => s.SongId == songId).FirstOrDefault();
        if (song == null)
        {
            return;
        }

        logger.LogWarning("Deleting song# {songId} at {path}", songId, song.Path);
        fileSource.SoftDelete(song.Path);
        IsDirty = true;
    }

    internal void UpdateSong(int songId, string name, string artist, string album)
    {
        lock (lockObject)
        {
            logger.LogInformation("Updating song# {songId} with name: {name}, artist: {artist}, album: {album}", songId, name, artist, album);

            // Find the song to get its file path
            var song = Songs.Where(s => s.SongId == songId).FirstOrDefault();
            if (song == null)
            {
                logger.LogWarning("Song with ID {songId} not found", songId);
                return;
            }

            // Update the MP3 file tags
            tagParser.UpdateSongTags(song.Path, name, artist, album);

            // Update the in-memory data store
            dataStore.UpdateSong(songId, name, artist, album);

            logger.LogInformation("Successfully updated song# {songId} both in file and in memory", songId);
        }
    }
}
