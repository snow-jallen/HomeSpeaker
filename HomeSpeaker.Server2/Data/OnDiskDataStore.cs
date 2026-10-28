using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Data;


public class OnDiskDataStore : IDataStore
{
    // Readers enumerate whatever list is current; rescans build a new list and swap it in, so nobody
    // ever enumerates a list that is being cleared or filled.
    private volatile List<Song> songs = new();

    public void Add(Song song)
    {
        var next = new List<Song>(songs);
        song.SongId = next.Count;
        next.Add(song);
        songs = next;
    }

    public void ReplaceAll(IEnumerable<Song> songs)
    {
        var next = songs.ToList();
        for (var i = 0; i < next.Count; i++)
        {
            next[i].SongId = i;
        }

        this.songs = next;
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
            var albumSongs = album.Songs.AsQueryable();
            yield return new Album
            {
                Name = album.AlbumName ?? string.Empty,
                Songs = albumSongs,
                Artist = new Artist
                {
                    Name = album.Songs.Select(s => s.Artist).FirstOrDefault(a => !string.IsNullOrEmpty(a)) ?? "[Artist Unknown]",
                    Albums = Enumerable.Empty<Album>().AsQueryable(),
                    Songs = albumSongs
                }
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
                Name = artist.ArtistName ?? "[Artist Unknown]",
                Albums = Enumerable.Empty<Album>().AsQueryable(),
                Songs = artist.Songs.AsQueryable()
            };
        }
    }

    public IEnumerable<Song> GetSongs() => songs;

    public void Clear() => songs = new();
}
