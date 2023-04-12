using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Data;

public interface IDataStore
{
    void Add(Song song);
    IEnumerable<Artist> GetArtists();
    IEnumerable<Album> GetAlbums();
    IEnumerable<Song> GetSongs();
    void Clear();
}
