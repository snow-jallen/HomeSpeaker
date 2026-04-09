using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Data;

public class MusicContext : DbContext
{
    public MusicContext(DbContextOptions<MusicContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RadioStream indexes
        modelBuilder.Entity<RadioStream>()
            .HasIndex(s => s.PlayCount)
            .IsDescending();

        modelBuilder.Entity<RadioStream>()
            .HasIndex(s => s.DisplayOrder);

        modelBuilder.Entity<RadioStream>()
            .HasIndex(s => s.Name);

        // Playlist indexes
        modelBuilder.Entity<Playlist>()
            .HasIndex(p => p.Name);

        // PlaylistItem indexes
        modelBuilder.Entity<PlaylistItem>()
            .HasIndex(pi => pi.Order);

        modelBuilder.Entity<PlaylistItem>()
            .HasIndex(pi => new { pi.PlaylistId, pi.SongPath });

        // Impression indexes
        modelBuilder.Entity<Impression>()
            .HasIndex(i => i.Timestamp);

        modelBuilder.Entity<Impression>()
            .HasIndex(i => i.SongPath);

        modelBuilder.Entity<Impression>()
            .HasIndex(i => i.PlayedBy);

        // Thumbnail indexes
        modelBuilder.Entity<Thumbnail>()
            .HasIndex(t => new { t.Artist, t.Album });

        // AnchorDefinitionEntity indexes
        modelBuilder.Entity<AnchorDefinitionEntity>()
            .HasIndex(ad => new { ad.IsActive, ad.Name });

        // UserAnchorEntity indexes
        modelBuilder.Entity<UserAnchorEntity>()
            .HasIndex(ua => new { ua.UserId, ua.AnchorDefinitionId })
            .IsUnique();

        // DailyAnchorEntity indexes
        modelBuilder.Entity<DailyAnchorEntity>()
            .HasIndex(da => new { da.UserId, da.Date });

        modelBuilder.Entity<DailyAnchorEntity>()
            .HasIndex(da => new { da.Date, da.IsCompleted });
    }
    public DbSet<Thumbnail> Thumbnails { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistItem> PlaylistItems { get; set; }
    public DbSet<Impression> Impressions { get; set; }
    public DbSet<AnchorDefinitionEntity> AnchorDefinitions { get; set; }
    public DbSet<UserAnchorEntity> UserAnchors { get; set; }
    public DbSet<DailyAnchorEntity> DailyAnchors { get; set; }
    public DbSet<RadioStream> RadioStreams { get; set; }
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
    public bool AlwaysShuffle { get; set; } = false;
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

public class RadioStream
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? FaviconFileName { get; set; }
    public int PlayCount { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}