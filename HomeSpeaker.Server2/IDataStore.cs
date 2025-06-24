using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public interface IDataStore
{
    void Add(Song song);
    void UpdateSong(int songId, string name, string artist, string album);
    IEnumerable<Artist> GetArtists();
    IEnumerable<Album> GetAlbums();
    IEnumerable<Song> GetSongs();
    void Clear();
}
