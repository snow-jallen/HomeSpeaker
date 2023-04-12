using HomeSpeaker.Server;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Data
{
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

        public IEnumerable<Artist> Artists
        {
            get
            {
                if (IsDirty)
                {
                    ResetLibrary();
                }
                return dataStore.GetArtists();
            }
        }

        public IEnumerable<Album> Albums
        {
            get
            {
                if (IsDirty)
                {
                    ResetLibrary();
                }
                return dataStore.GetAlbums();
            }
        }

        public IEnumerable<Song> Songs
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
    }
}
