using HomeSpeaker.Server2;

namespace HomeSpeaker.Server2;

public class Mp3Library
{
    private readonly IFileSource _fileSource;
    private readonly ITagParser _tagParser;
    private readonly IDataStore _dataStore;
    private readonly ILogger<Mp3Library> _logger;
    private readonly object lockObject = new();

    public Mp3Library(IFileSource fileSource, ITagParser tagParser, IDataStore dataStore, ILogger<Mp3Library> logger)
    {
        _fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
        _tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation($"Initialized with fileSource {fileSource.RootFolder}");

        SyncLibrary();
    }

    public string RootFolder => _fileSource.RootFolder;

    public void SyncLibrary()
    {
        lock (lockObject)
        {
            _logger.LogInformation("Synchronizing MP3 library - reloading from disk.");
            _dataStore.Clear();
            var files = _fileSource.GetAllMp3s();
            foreach (var file in files)
            {
                try
                {
                    var song = _tagParser.CreateSong(file);
                    _dataStore.Add(song);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Trouble parsing tag info!");
                }
            }
            _logger.LogInformation("Sync Completed! {count} songs in database.", _dataStore.GetSongs().Count());
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
            return _dataStore.GetSongs();
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
        if (song == null)
        {
            return;
        }

        _logger.LogWarning("Deleting song# {songId} at {path}", songId, song.Path);
        _fileSource.SoftDelete(song.Path);
        IsDirty = true;
    }

    internal void UpdateSong(int songId, string name, string artist, string album)
    {
        lock (lockObject)
        {
            _logger.LogInformation("Updating song# {songId} with name: {name}, artist: {artist}, album: {album}", songId, name, artist, album);

            // Find the song to get its file path
            var song = Songs.Where(s => s.SongId == songId).FirstOrDefault();
            if (song == null)
            {
                _logger.LogWarning("Song with ID {songId} not found", songId);
                return;
            }

            // Update the MP3 file tags
            _tagParser.UpdateSongTags(song.Path, name, artist, album);

            // Update the in-memory data store
            _dataStore.UpdateSong(songId, name, artist, album);

            _logger.LogInformation("Successfully updated song# {songId} both in file and in memory", songId);
        }
    }
}
