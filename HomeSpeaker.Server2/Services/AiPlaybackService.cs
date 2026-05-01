using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public sealed class AiPlaybackService
{
    private const int DefaultQueueSize = 40;
    private readonly MusicContext dbContext;
    private readonly Mp3Library library;
    private readonly IMusicPlayer musicPlayer;
    private readonly TimeProvider timeProvider;

    public AiPlaybackService(MusicContext dbContext, Mp3Library library, IMusicPlayer musicPlayer, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.library = library;
        this.musicPlayer = musicPlayer;
        this.timeProvider = timeProvider;
    }

    public async Task<AiPlayerContextDto?> GetCurrentContextAsync(CancellationToken cancellationToken)
    {
        var session = await dbContext.AiPlaybackSessions.AsNoTracking()
            .OrderByDescending(s => s.StartedUtc)
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var seedSongId = session.SeedSongPath != null
            ? library.Songs.FirstOrDefault(s => s.Path == session.SeedSongPath)?.SongId
            : null;

        return new AiPlayerContextDto
        {
            Mode = session.Mode.ToString(),
            SessionId = session.SessionId.ToString(),
            GenreKey = session.GenreKey,
            SeedSongId = seedSongId,
            AllowFeedback = true
        };
    }

    public async Task<AiPlayerContextDto?> StartGenreSessionAsync(string genreKey, CancellationToken cancellationToken)
    {
        var songs = await getGenreSongsAsync(genreKey, cancellationToken);
        if (songs.Count == 0)
        {
            return null;
        }

        musicPlayer.Stop();
        musicPlayer.ClearQueue();
        musicPlayer.PlaySong(songs[0]);
        foreach (var song in songs.Skip(1))
        {
            musicPlayer.EnqueueSong(song);
        }

        var session = await createSessionAsync(AiPlaybackMode.Genre, genreKey, songs[0].Path, cancellationToken);
        return new AiPlayerContextDto
        {
            Mode = session.Mode.ToString(),
            SessionId = session.SessionId.ToString(),
            GenreKey = session.GenreKey,
            SeedSongId = songs[0].SongId,
            AllowFeedback = true
        };
    }

    public async Task<AiPlayerContextDto?> StartSimilarSessionFromCurrentAsync(CancellationToken cancellationToken)
    {
        var currentSong = musicPlayer.Status.CurrentSong;
        if (currentSong?.Path == null)
        {
            return null;
        }

        var songs = await getSimilarSongsAsync(currentSong.Path, cancellationToken);
        if (songs.Count == 0)
        {
            return null;
        }

        musicPlayer.ClearQueue();
        foreach (var song in songs)
        {
            musicPlayer.EnqueueSong(song);
        }

        var session = await createSessionAsync(AiPlaybackMode.Similar, null, currentSong.Path, cancellationToken);
        return new AiPlayerContextDto
        {
            Mode = session.Mode.ToString(),
            SessionId = session.SessionId.ToString(),
            SeedSongId = currentSong.SongId,
            AllowFeedback = true
        };
    }

    public async Task SubmitFeedbackAsync(AiFeedbackRequest request, CancellationToken cancellationToken)
    {
        AiPlaybackSession? session = null;
        if (!string.IsNullOrWhiteSpace(request.SessionId)
            && Guid.TryParse(request.SessionId, out var requestedSessionId))
        {
            session = await dbContext.AiPlaybackSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == requestedSessionId, cancellationToken);
        }

        if (session == null)
        {
            session = await dbContext.AiPlaybackSessions.AsNoTracking()
                .OrderByDescending(s => s.StartedUtc)
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

            if (session == null)
            {
                return;
            }
        }

        var sessionId = session.SessionId;

        var song = library.Songs.FirstOrDefault(s => s.SongId == request.SongId);
        if (song?.Path == null)
        {
            return;
        }

        var feedback = request.Feedback.Equals("up", StringComparison.OrdinalIgnoreCase)
            ? AiFeedbackType.Up
            : AiFeedbackType.Down;

        dbContext.AiPlaybackFeedbacks.Add(new AiPlaybackFeedback
        {
            SessionId = sessionId,
            SongPath = song.Path,
            Feedback = feedback,
            PreviousSongPath = request.PreviousSongId.HasValue
                ? library.Songs.FirstOrDefault(s => s.SongId == request.PreviousSongId.Value)?.Path
                : null,
            GenreKey = session?.GenreKey,
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Song>> getGenreSongsAsync(string genreKey, CancellationToken cancellationToken)
    {
        var scores = await dbContext.AiTrackGenreScores.AsNoTracking()
            .Where(s => s.GenreKey == genreKey)
            .OrderByDescending(s => s.Score)
            .Take(DefaultQueueSize)
            .ToListAsync(cancellationToken);

        var songsByPath = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToDictionary(s => s.Path!, s => s);

        var scoredSongs = scores
            .Select(score => new ScoredSong(score.Score, songsByPath.GetValueOrDefault(score.SongPath)))
            .Where(entry => entry.Song != null)
            .Select(entry => entry!)
            .ToList();

        return await applyFeedbackBiasAsync(scoredSongs, cancellationToken);
    }

    private async Task<List<Song>> getSimilarSongsAsync(string songPath, CancellationToken cancellationToken)
    {
        var similarities = await dbContext.AiTrackSimilarities.AsNoTracking()
            .Where(s => s.SongPath == songPath)
            .OrderByDescending(s => s.Score)
            .Take(DefaultQueueSize)
            .ToListAsync(cancellationToken);

        var songsByPath = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToDictionary(s => s.Path!, s => s);

        var scoredSongs = similarities
            .Select(sim => new ScoredSong(sim.Score, songsByPath.GetValueOrDefault(sim.SimilarSongPath)))
            .Where(entry => entry.Song != null)
            .Select(entry => entry!)
            .ToList();

        return await applyFeedbackBiasAsync(scoredSongs, cancellationToken);
    }

    private async Task<List<Song>> applyFeedbackBiasAsync(List<ScoredSong> songs, CancellationToken cancellationToken)
    {
        if (songs.Count == 0)
        {
            return new List<Song>();
        }

        var paths = songs.Select(s => s.Song.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var feedbacks = await dbContext.AiPlaybackFeedbacks.AsNoTracking()
            .Where(f => paths.Contains(f.SongPath))
            .GroupBy(f => f.SongPath)
            .Select(g => new
            {
                SongPath = g.Key,
                UpCount = g.Count(f => f.Feedback == AiFeedbackType.Up),
                DownCount = g.Count(f => f.Feedback == AiFeedbackType.Down)
            })
            .ToListAsync(cancellationToken);

        var feedbackLookup = feedbacks.ToDictionary(f => f.SongPath, f => (f.UpCount, f.DownCount));

        foreach (var song in songs)
        {
            song.BiasedScore = song.BaseScore + getFeedbackScore(song.Song.Path, feedbackLookup);
        }

        return songs
            .OrderByDescending(s => s.BiasedScore)
            .Select(s => s.Song)
            .ToList();
    }

    private static double getFeedbackScore(string? songPath, Dictionary<string, (int UpCount, int DownCount)> lookup)
    {
        if (songPath == null || !lookup.TryGetValue(songPath, out var counts))
        {
            return 0;
        }

        return counts.UpCount * 0.2 - counts.DownCount * 0.5;
    }

    private sealed class ScoredSong
    {
        public ScoredSong(double baseScore, Song? song)
        {
            BaseScore = baseScore;
            Song = song ?? throw new ArgumentNullException(nameof(song));
        }

        public double BaseScore { get; }
        public Song Song { get; }
        public double BiasedScore { get; set; }
    }

    private async Task<AiPlaybackSession> createSessionAsync(
        AiPlaybackMode mode,
        string? genreKey,
        string? seedSongPath,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activeSessions = await dbContext.AiPlaybackSessions
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var active in activeSessions)
        {
            active.IsActive = false;
            active.LastAdvancedUtc = now;
        }

        var session = new AiPlaybackSession
        {
            SessionId = Guid.NewGuid(),
            Mode = mode,
            GenreKey = genreKey,
            SeedSongPath = seedSongPath,
            StartedUtc = now,
            LastAdvancedUtc = now,
            IsActive = true
        };

        dbContext.AiPlaybackSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }
}
