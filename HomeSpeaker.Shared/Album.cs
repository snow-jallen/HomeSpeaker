namespace HomeSpeaker.Shared;

public class Album
{
    public int AlbumId { get; set; }
    public required string Name { get; set; }
    public required IQueryable<Song> Songs { get; set; }
    public required Artist Artist { get; set; }
    public int ArtistId { get; set; }
}
