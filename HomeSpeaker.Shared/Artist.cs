using System.Linq;

namespace HomeSpeaker.Shared;

public class Artist
{
    public int ArtistId { get; set; }
    public string Name { get; set; }
    public IQueryable<Album> Albums { get; set; }
    public IQueryable<Song> Songs { get; set; }
}
