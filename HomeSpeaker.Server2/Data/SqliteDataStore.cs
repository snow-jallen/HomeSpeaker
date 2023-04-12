using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Data;

public class SqliteDataStore : IDataStore
{
    private readonly MusicContext context;

    public SqliteDataStore(MusicContext context)
    {
        this.context = context;
    }

    public void Add(Song song)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Album> GetAlbums()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Artist> GetArtists()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Song> GetSongs()
    {
        throw new NotImplementedException();
    }
}