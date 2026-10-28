using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public interface IDataStore
{
    void Add(Song song);
    /// <summary>Swaps in a complete new song list at once, renumbering <see cref="Song.SongId"/> from 0.</summary>
    void ReplaceAll(IEnumerable<Song> songs);
    void UpdateSong(int songId, string name, string artist, string album);
    IEnumerable<Artist> GetArtists();
    IEnumerable<Album> GetAlbums();
    IEnumerable<Song> GetSongs();
    void Clear();
}
