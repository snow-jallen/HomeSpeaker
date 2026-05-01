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

        modelBuilder.Entity<AiGenreDefinition>()
            .HasKey(g => g.Key);

        modelBuilder.Entity<AiGenreDefinition>()
            .HasIndex(g => g.SortOrder);

        modelBuilder.Entity<AiTrackProfile>()
            .HasKey(p => p.SongPath);

        modelBuilder.Entity<AiTrackProfile>()
            .Property(p => p.Status)
            .HasConversion<string>();

        modelBuilder.Entity<AiTrackProfile>()
            .HasIndex(p => new { p.Status, p.LastAnalyzedUtc });

        modelBuilder.Entity<AiTrackMarker>()
            .HasIndex(m => new { m.SongPath, m.MarkerKey });

        modelBuilder.Entity<AiTrackGenreScore>()
            .HasKey(g => new { g.SongPath, g.GenreKey });

        modelBuilder.Entity<AiTrackGenreScore>()
            .HasIndex(g => g.GenreKey);

        modelBuilder.Entity<AiTrackSimilarity>()
            .HasKey(s => new { s.SongPath, s.SimilarSongPath });

        modelBuilder.Entity<AiTrackSimilarity>()
            .HasIndex(s => new { s.SongPath, s.Score });

        modelBuilder.Entity<AiProcessingWorkItem>()
            .Property(w => w.Status)
            .HasConversion<string>();

        modelBuilder.Entity<AiProcessingWorkItem>()
            .HasIndex(w => w.SongPath)
            .IsUnique();

        modelBuilder.Entity<AiProcessingWorkItem>()
            .HasIndex(w => new { w.Status, w.LeaseExpiresUtc });

        modelBuilder.Entity<AiProcessingRun>()
            .Property(r => r.State)
            .HasConversion<string>();

        modelBuilder.Entity<AiProcessingRun>()
            .HasIndex(r => r.State);

        modelBuilder.Entity<AiPlaybackSession>()
            .HasKey(s => s.SessionId);

        modelBuilder.Entity<AiPlaybackSession>()
            .Property(s => s.Mode)
            .HasConversion<string>();

        modelBuilder.Entity<AiPlaybackSession>()
            .HasIndex(s => new { s.IsActive, s.StartedUtc });

        modelBuilder.Entity<AiPlaybackFeedback>()
            .Property(f => f.Feedback)
            .HasConversion<string>();

        modelBuilder.Entity<AiPlaybackFeedback>()
            .HasIndex(f => f.SessionId);

        modelBuilder.Entity<AiPlaybackFeedback>()
            .HasIndex(f => f.SongPath);

        modelBuilder.Entity<AiGenreDefinition>().HasData(
            new AiGenreDefinition
            {
                Key = "peaceful-instrumental",
                DisplayName = "Peaceful Instrumental",
                Description = "Calm instrumental tracks for quiet moments.",
                SortOrder = 1,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "quiet-sunday",
                DisplayName = "Quiet Sunday",
                Description = "Gentle vocals and soft arrangements for restful days.",
                SortOrder = 2,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "driving-tunes",
                DisplayName = "Driving Tunes",
                Description = "Steady rhythm and forward momentum for the road.",
                SortOrder = 3,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "choral",
                DisplayName = "Choral",
                Description = "Choral harmonies and choir-led arrangements.",
                SortOrder = 4,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "upbeat-a-cappella",
                DisplayName = "Upbeat A Cappella",
                Description = "Vocal-driven, energetic a cappella performances.",
                SortOrder = 5,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "country",
                DisplayName = "Country",
                Description = "Country storytelling with warm acoustic textures.",
                SortOrder = 6,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "quiet-classical",
                DisplayName = "Quiet Classical",
                Description = "Soft classical pieces and reflective orchestral work.",
                SortOrder = 7,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "church-christmas",
                DisplayName = "Church Christmas",
                Description = "Traditional church Christmas recordings and arrangements.",
                SortOrder = 8,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "hymns",
                DisplayName = "Hymns",
                Description = "Classic hymns and worship standards.",
                SortOrder = 9,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "classical-christmas",
                DisplayName = "Classical Christmas",
                Description = "Classical takes on holiday repertoire.",
                SortOrder = 10,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "vocal-christmas",
                DisplayName = "Vocal Christmas",
                Description = "Vocal-forward holiday performances.",
                SortOrder = 11,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "worship-ensemble",
                DisplayName = "Worship Ensemble",
                Description = "Full-band worship and ensemble recordings.",
                SortOrder = 12,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "reflective-piano",
                DisplayName = "Reflective Piano",
                Description = "Solo piano and reflective keys-driven pieces.",
                SortOrder = 13,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "family-singalong",
                DisplayName = "Family Singalong",
                Description = "Upbeat, communal songs for family listening.",
                SortOrder = 14,
                IsActive = true
            },
            new AiGenreDefinition
            {
                Key = "warm-folk-acoustic",
                DisplayName = "Warm Folk Acoustic",
                Description = "Warm acoustic folk with organic instrumentation.",
                SortOrder = 15,
                IsActive = true
            });
    }
    public DbSet<Thumbnail> Thumbnails { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistItem> PlaylistItems { get; set; }
    public DbSet<Impression> Impressions { get; set; }
    public DbSet<AnchorDefinitionEntity> AnchorDefinitions { get; set; }
    public DbSet<UserAnchorEntity> UserAnchors { get; set; }
    public DbSet<DailyAnchorEntity> DailyAnchors { get; set; }
    public DbSet<RadioStream> RadioStreams { get; set; }
    public DbSet<AiGenreDefinition> AiGenreDefinitions { get; set; }
    public DbSet<AiTrackProfile> AiTrackProfiles { get; set; }
    public DbSet<AiTrackMarker> AiTrackMarkers { get; set; }
    public DbSet<AiTrackGenreScore> AiTrackGenreScores { get; set; }
    public DbSet<AiTrackSimilarity> AiTrackSimilarities { get; set; }
    public DbSet<AiProcessingWorkItem> AiProcessingWorkItems { get; set; }
    public DbSet<AiProcessingRun> AiProcessingRuns { get; set; }
    public DbSet<AiPlaybackSession> AiPlaybackSessions { get; set; }
    public DbSet<AiPlaybackFeedback> AiPlaybackFeedbacks { get; set; }
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
    public bool AlwaysShuffle { get; set; }
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

public class AiGenreDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class AiTrackProfile
{
    public string SongPath { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string AnalysisVersion { get; set; } = string.Empty;
    public AiProcessingStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAnalyzedUtc { get; set; }
    public string? Summary { get; set; }
    public string? TempoLabel { get; set; }
    public string? PrimaryMood { get; set; }
    public double Energy { get; set; }
    public double Acousticness { get; set; }
    public double Instrumentalness { get; set; }
    public double VocalPresence { get; set; }
    public double Sacredness { get; set; }
    public double SeasonalityChristmas { get; set; }
    public double Danceability { get; set; }
    public double Warmth { get; set; }
    public double Confidence { get; set; }
}

public class AiTrackMarker
{
    public int Id { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public string MarkerKey { get; set; } = string.Empty;
    public double MarkerValue { get; set; }
    public double Confidence { get; set; }
}

public class AiTrackGenreScore
{
    public string SongPath { get; set; } = string.Empty;
    public string GenreKey { get; set; } = string.Empty;
    public double Score { get; set; }
    public int Rank { get; set; }
    public string? Why { get; set; }
}

public class AiTrackSimilarity
{
    public string SongPath { get; set; } = string.Empty;
    public string SimilarSongPath { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? ReasonsJson { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class AiProcessingWorkItem
{
    public Guid Id { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public AiProcessingStatus Status { get; set; }
    public string? BatchId { get; set; }
    public DateTime? LeaseExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime QueuedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? LastError { get; set; }
}

public class AiProcessingRun
{
    public int Id { get; set; }
    public AiProcessingState State { get; set; }
    public int TotalTracks { get; set; }
    public int QueuedTracks { get; set; }
    public int ProcessingTracks { get; set; }
    public int CompletedTracks { get; set; }
    public int FailedTracks { get; set; }
    public string? CurrentBatchId { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime? LastScanUtc { get; set; }
}

public class AiPlaybackSession
{
    public Guid SessionId { get; set; }
    public AiPlaybackMode Mode { get; set; }
    public string? GenreKey { get; set; }
    public string? SeedSongPath { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? LastAdvancedUtc { get; set; }
    public bool IsActive { get; set; }
}

public class AiPlaybackFeedback
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public string SongPath { get; set; } = string.Empty;
    public AiFeedbackType Feedback { get; set; }
    public string? PreviousSongPath { get; set; }
    public string? GenreKey { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public enum AiProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public enum AiProcessingState
{
    Idle,
    Scanning,
    Processing,
    Degraded
}

public enum AiPlaybackMode
{
    Genre,
    Similar
}

public enum AiFeedbackType
{
    Up,
    Down
}
