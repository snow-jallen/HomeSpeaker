using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Data;

public class MusicContext : DbContext
{
    public MusicContext(DbContextOptions<MusicContext> options) : base(options)
    {

    }    public DbSet<Thumbnail> Thumbnails { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistItem> PlaylistItems { get; set; }
    public DbSet<Impression> Impressions { get; set; }
    public DbSet<AnchorDefinitionEntity> AnchorDefinitions { get; set; }
    public DbSet<UserAnchorEntity> UserAnchors { get; set; }
    public DbSet<DailyAnchorEntity> DailyAnchors { get; set; }
    public DbSet<SongGenre> SongGenres { get; set; }
}

public class Thumbnail
{
    public int Id { get; set; }
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}

public class Playlist
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PlaylistItem> Songs { get; set; } = new();
}

public class PlaylistItem
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class Impression
{
    public int Id { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string PlayedBy { get; set; } = string.Empty;
}

/// <summary>
/// Entity for anchor definitions (the template)
/// </summary>
public class AnchorDefinitionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}

/// <summary>
/// Entity for user's current anchors (current active assignments)
/// </summary>
public class UserAnchorEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int AnchorDefinitionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public AnchorDefinitionEntity? AnchorDefinition { get; set; }
}

/// <summary>
/// Entity for daily anchor snapshots (temporal records)
/// </summary>
public class DailyAnchorEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int AnchorDefinitionId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string AnchorName { get; set; } = string.Empty; // Snapshot of name at time of creation
    public string AnchorDescription { get; set; } = string.Empty; // Snapshot of description at time of creation
    public DateTime CreatedAt { get; set; }
}

public class SongGenre
{
    public int Id { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
}