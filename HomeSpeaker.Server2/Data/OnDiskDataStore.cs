using HomeSpeaker.Server2;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server.Data;


public class OnDiskDataStore : IDataStore
{
    public OnDiskDataStore()
    {
        songs = new();
    }

    private List<Song> songs;    public void Add(Song song)
    {
        song.SongId = songs.Count;
        songs.Add(song);
    }

    public void UpdateSong(int songId, string name, string artist, string album)
    {
        var song = songs.FirstOrDefault(s => s.SongId == songId);
        if (song != null)
        {
            song.Name = name;
            song.Artist = artist;
            song.Album = album;
        }
    }

    public IEnumerable<Album> GetAlbums()
    {
        foreach (var album in from s in songs
                              group s by s.Album into albums
                              orderby albums.Key
                              select new { AlbumName = albums.Key, Songs = albums })
        {
            yield return new Album
            {
                Name = album.AlbumName,
                Songs = album.Songs.AsQueryable()
            };
        }
    }

    public IEnumerable<Artist> GetArtists()
    {
        foreach (var artist in from s in songs
                               group s by s.Artist into artists
                               orderby artists.Key
                               select new { ArtistName = artists.Key, Songs = artists })
        {
            yield return new Artist
            {
                Name = artist.ArtistName,
                Songs = artist.Songs.AsQueryable()
            };
        }
    }

    public IEnumerable<Song> GetSongs() => songs.AsEnumerable();

    public void Clear() => songs.Clear();
}
