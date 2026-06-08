namespace HomeSpeaker.Shared;

public class Artist
{
    public int ArtistId { get; set; }
    public required string Name { get; set; }
    public required IQueryable<Album> Albums { get; set; }
    public required IQueryable<Song> Songs { get; set; }
}
