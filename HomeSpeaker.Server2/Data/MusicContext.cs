using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Data;

public class MusicContext : DbContext
{
    public MusicContext(DbContextOptions<MusicContext> options) : base(options)
    {
        
    }

    public DbSet<DbSong> Songs { get; set; }
    public DbSet<DbAlbum> Albums { get; set; }
    public DbSet<DbArtist> Artists { get; set; }
    public DbSet<DbPlaylist> Playlists { get; set; }
    public DbSet<DbPlaylistItem> PlaylistItems { get; set; }
    public DbSet<DbImpression> Impressions { get; set; }
}

public class DbSong
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DbAlbum Album { get; set; }
    public string RelativePath { get; set; }

}

public class DbDeletedSong
{
    public int Id { get; set; }
    public string Title { get; set; }
    public DbAlbum Album { get; set; }
    public string RelativePath { get; set; }
    public DateTime DeletedOn { get; set; }
    public string DeletedBy { get; set; }
}

public class DbAlbum
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required int DbArtistId { get; set; }
    public string ThumbnailUrl { get; set; }
    public List<DbSong> Songs { get; set; }
}

public class DbArtist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<DbAlbum> Albums { get; set; }
}

public class DbPlaylist
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

}

public class DbPlaylistItem
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public int DbSongId { get; set; }
    public int Order { get; set; }
}

public class DbImpression
{
    public int Id { get; set; }
    public int DbSongId { get; set; }
    public DateTime Timestamp { get; set; }
    public string PlayedBy { get; set; }
}