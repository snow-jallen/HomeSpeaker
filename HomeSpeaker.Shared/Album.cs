using System.Linq;

namespace HomeSpeaker.Shared;

public class Album
{
    public int AlbumId { get; set; }
    public string Name { get; set; }
    public IQueryable<Song> Songs { get; set; }
    public Artist Artist { get; set; }
    public int ArtistId { get; set; }
}
