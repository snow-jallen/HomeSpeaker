using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>
/// Rebuilds the automatic playlists (<see cref="SmartPlaylistRules"/>) a few minutes after startup and
/// every few hours after that. They are ordinary playlists marked with <see cref="Data.Playlist.AutoKind"/>,
/// so playing them, autoplay, Siri and the iPhone app all work unchanged; their songs are replaced on
/// every refresh. A playlist someone made with the same name is left alone.
/// </summary>
public sealed class SmartPlaylistWorker : BackgroundService
{
    private static readonly TimeSpan firstRunDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan refreshInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly Mp3Library library;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SmartPlaylistWorker> logger;

    public SmartPlaylistWorker(IServiceScopeFactory scopeFactory, Mp3Library library, TimeProvider timeProvider, ILogger<SmartPlaylistWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.library = library;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(firstRunDelay, timeProvider, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await refreshAsync(stoppingToken);
                await Task.Delay(refreshInterval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task refreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
            var history = await db.Impressions
                .AsNoTracking()
                .Select(i => new PlayRecord(i.SongPath, i.Timestamp, i.Skipped))
                .ToListAsync(cancellationToken);
            var songPaths = library.Songs.Select(s => s.Path).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var playlists = SmartPlaylistRules.Build(history, songPaths, timeProvider.GetUtcNow().UtcDateTime);
            await ApplyAsync(db, playlists, logger, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Couldn't refresh the automatic playlists");
        }
    }

    /// <summary>Writes the automatic playlists into the database, creating each once it has enough songs.</summary>
    public static async Task ApplyAsync(MusicContext db, IEnumerable<SmartPlaylist> playlists, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var smart in playlists)
        {
            var playlist = await db.Playlists.Include(p => p.Songs).FirstOrDefaultAsync(p => p.AutoKind == smart.Kind, cancellationToken);
            if (playlist is null)
            {
                if (smart.SongPaths.Count < SmartPlaylistRules.MinimumSongsToCreate)
                {
                    continue;
                }

                if (await db.Playlists.AnyAsync(p => p.Name == smart.Name, cancellationToken))
                {
                    logger.LogInformation("Not creating the automatic playlist {Name}: a playlist with that name already exists", smart.Name);
                    continue;
                }

                playlist = new Data.Playlist { Name = smart.Name, AutoKind = smart.Kind };
                db.Playlists.Add(playlist);
            }

            db.PlaylistItems.RemoveRange(playlist.Songs);
            playlist.Songs = smart.SongPaths.Select((path, index) => new PlaylistItem { SongPath = path, Order = index }).ToList();
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Refreshed automatic playlist {Name} with {Count} songs", smart.Name, smart.SongPaths.Count);
        }
    }
}
