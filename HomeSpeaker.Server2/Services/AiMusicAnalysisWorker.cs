using System.Text.Json;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class AiMusicAnalysisWorker : BackgroundService
{
    private const int SimilarityNeighbors = 30;
    private readonly IServiceProvider serviceProvider;
    private readonly AiProcessingSignal processingSignal;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AiMusicAnalysisWorker> logger;

    public AiMusicAnalysisWorker(
        IServiceProvider serviceProvider,
        AiProcessingSignal processingSignal,
        TimeProvider timeProvider,
        ILogger<AiMusicAnalysisWorker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.processingSignal = processingSignal;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await runOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI analysis worker encountered an error");
            }

            await waitForNextCycleAsync(stoppingToken);
        }
    }

    private async Task waitForNextCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AiMusicOptions>>().Value;
        var delay = await getNextCycleDelayAsync(dbContext, options, stoppingToken);
        await processingSignal.WaitForNextAsync(delay, stoppingToken);
    }

    private async Task runOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicContext>();
        var library = scope.ServiceProvider.GetRequiredService<Mp3Library>();
        var analyzer = scope.ServiceProvider.GetRequiredService<AiMusicAnalyzer>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AiMusicOptions>>().Value;

        if (!options.Processing.Enabled)
        {
            await updateRunStateAsync(dbContext, AiProcessingState.Idle, stoppingToken);
            return;
        }

        if (!options.HasConfiguredProvider)
        {
            await updateRunStateAsync(dbContext, AiProcessingState.Degraded, stoppingToken);
            return;
        }

        await updateRunStateAsync(dbContext, AiProcessingState.Scanning, stoppingToken);
        await resetExpiredLeasesAsync(dbContext, stoppingToken);
        await requeueFailedItemsAsync(dbContext, options, stoppingToken);
        await scanLibraryAsync(dbContext, library, options, stoppingToken);

        var batchCount = Math.Max(1, options.Processing.MaxParallelBatches);
        for (var i = 0; i < batchCount; i++)
        {
            var workItems = await claimBatchAsync(dbContext, options, stoppingToken);
            if (workItems.Count == 0)
            {
                break;
            }

            await updateRunStateAsync(dbContext, AiProcessingState.Processing, stoppingToken, workItems[0].BatchId);
            await processBatchAsync(dbContext, library, analyzer, workItems, options, stoppingToken);
        }

        var remainingWork = await dbContext.AiProcessingWorkItems
            .AnyAsync(w => w.Status == AiProcessingStatus.Pending || w.Status == AiProcessingStatus.Processing, stoppingToken);
        var failedAwaitingRetry = await dbContext.AiProcessingWorkItems
            .AnyAsync(w => w.Status == AiProcessingStatus.Failed, stoppingToken);
        var nextState = remainingWork
            ? AiProcessingState.Processing
            : failedAwaitingRetry
                ? AiProcessingState.Degraded
                : AiProcessingState.Idle;
        await updateRunStateAsync(dbContext, nextState, stoppingToken);
    }

    private async Task<TimeSpan> getNextCycleDelayAsync(
        MusicContext dbContext,
        AiMusicOptions options,
        CancellationToken cancellationToken)
    {
        var scanDelay = TimeSpan.FromMinutes(Math.Max(1, options.Processing.ScanIntervalMinutes));
        var retryDelay = TimeSpan.FromMinutes(Math.Max(1, options.Processing.FailedItemRequeueDelayMinutes));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var failedCompletionTimes = await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => w.Status == AiProcessingStatus.Failed && w.CompletedUtc != null)
            .Select(w => w.CompletedUtc)
            .ToListAsync(cancellationToken);

        var nextRetryDelay = failedCompletionTimes
            .Where(completedUtc => completedUtc.HasValue)
            .Select(completedUtc => completedUtc!.Value.Add(retryDelay) - now)
            .Where(delay => delay > TimeSpan.Zero)
            .DefaultIfEmpty(scanDelay)
            .Min();

        return nextRetryDelay < scanDelay ? nextRetryDelay : scanDelay;
    }

    private async Task updateRunStateAsync(
        MusicContext dbContext,
        AiProcessingState state,
        CancellationToken cancellationToken,
        string? batchId = null)
    {
        var run = await dbContext.AiProcessingRuns.FirstOrDefaultAsync(cancellationToken)
            ?? new AiProcessingRun { State = state };

        if (run.Id == 0)
        {
            dbContext.AiProcessingRuns.Add(run);
        }

        run.State = state;
        run.CurrentBatchId = batchId;
        run.LastHeartbeatUtc = timeProvider.GetUtcNow().UtcDateTime;
        await updateCountsAsync(dbContext, run, cancellationToken);
    }

    private async Task updateCountsAsync(MusicContext dbContext, AiProcessingRun run, CancellationToken cancellationToken)
    {
        run.QueuedTracks = await dbContext.AiProcessingWorkItems.CountAsync(w => w.Status == AiProcessingStatus.Pending, cancellationToken);
        run.ProcessingTracks = await dbContext.AiProcessingWorkItems.CountAsync(w => w.Status == AiProcessingStatus.Processing, cancellationToken);
        run.CompletedTracks = await dbContext.AiTrackProfiles.CountAsync(p => p.Status == AiProcessingStatus.Completed, cancellationToken);
        run.FailedTracks = await dbContext.AiTrackProfiles.CountAsync(p => p.Status == AiProcessingStatus.Failed, cancellationToken);
        run.TotalTracks = await dbContext.AiTrackProfiles.CountAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task resetExpiredLeasesAsync(MusicContext dbContext, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expired = await dbContext.AiProcessingWorkItems
            .Where(w => w.Status == AiProcessingStatus.Processing && w.LeaseExpiresUtc < now)
            .ToListAsync(cancellationToken);

        foreach (var item in expired)
        {
            item.Status = AiProcessingStatus.Pending;
            item.BatchId = null;
            item.LeaseExpiresUtc = null;
            item.LastError = "Lease expired; re-queued.";
        }

        if (expired.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task requeueFailedItemsAsync(
        MusicContext dbContext,
        AiMusicOptions options,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var retryCutoffUtc = now.AddMinutes(-Math.Max(1, options.Processing.FailedItemRequeueDelayMinutes));
        var failedItems = await dbContext.AiProcessingWorkItems
            .Where(w => w.Status == AiProcessingStatus.Failed && w.CompletedUtc != null && w.CompletedUtc <= retryCutoffUtc)
            .OrderBy(w => w.CompletedUtc)
            .ToListAsync(cancellationToken);

        foreach (var item in failedItems)
        {
            item.Status = AiProcessingStatus.Pending;
            item.BatchId = null;
            item.LeaseExpiresUtc = null;
            item.StartedUtc = null;
            item.QueuedUtc = now;
        }

        if (failedItems.Count > 0)
        {
            logger.LogInformation(
                "Re-queued {Count} failed AI analysis item(s) after cooldown of {DelayMinutes} minute(s)",
                failedItems.Count,
                Math.Max(1, options.Processing.FailedItemRequeueDelayMinutes));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task scanLibraryAsync(
        MusicContext dbContext,
        Mp3Library library,
        AiMusicOptions options,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var songs = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToList();

        var profiles = await dbContext.AiTrackProfiles.ToDictionaryAsync(p => p.SongPath, cancellationToken);
        var workItems = await dbContext.AiProcessingWorkItems.ToDictionaryAsync(w => w.SongPath, cancellationToken);

        foreach (var song in songs)
        {
            var fingerprint = computeFingerprint(song);
            if (!profiles.TryGetValue(song.Path!, out var profile))
            {
                profile = new AiTrackProfile
                {
                    SongPath = song.Path!,
                    Fingerprint = fingerprint,
                    AnalysisVersion = options.AnalysisVersion,
                    Status = AiProcessingStatus.Pending,
                    Attempts = 0
                };
                dbContext.AiTrackProfiles.Add(profile);
                profiles[song.Path!] = profile;
            }
            else if (profile.Fingerprint != fingerprint || profile.AnalysisVersion != options.AnalysisVersion)
            {
                profile.Fingerprint = fingerprint;
                profile.AnalysisVersion = options.AnalysisVersion;
                profile.Status = AiProcessingStatus.Pending;
                profile.LastError = null;
            }

            if (!workItems.TryGetValue(song.Path!, out var workItem))
            {
                workItem = new AiProcessingWorkItem
                {
                    Id = Guid.NewGuid(),
                    SongPath = song.Path!,
                    Fingerprint = fingerprint,
                    Status = AiProcessingStatus.Pending,
                    QueuedUtc = now,
                    Attempts = 0
                };
                dbContext.AiProcessingWorkItems.Add(workItem);
                workItems[song.Path!] = workItem;
            }
            else if (workItem.Fingerprint != fingerprint)
            {
                workItem.Fingerprint = fingerprint;
                workItem.Status = AiProcessingStatus.Pending;
                workItem.LeaseExpiresUtc = null;
                workItem.BatchId = null;
                workItem.LastError = null;
            }
        }

        var run = await dbContext.AiProcessingRuns.FirstOrDefaultAsync(cancellationToken);
        run?.LastScanUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<AiProcessingWorkItem>> claimBatchAsync(
        MusicContext dbContext,
        AiMusicOptions options,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batchSize = Math.Max(1, options.Processing.BatchSize);
        var leaseExpires = now.AddMinutes(Math.Max(1, options.Processing.StaleLeaseMinutes));
        var batchId = Guid.NewGuid().ToString("N");

        var pending = await dbContext.AiProcessingWorkItems
            .Where(w => w.Status == AiProcessingStatus.Pending)
            .OrderBy(w => w.QueuedUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return pending;
        }

        foreach (var item in pending)
        {
            item.Status = AiProcessingStatus.Processing;
            item.BatchId = batchId;
            item.LeaseExpiresUtc = leaseExpires;
            item.StartedUtc = now;
            item.Attempts += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pending;
    }

    private async Task processBatchAsync(
        MusicContext dbContext,
        Mp3Library library,
        AiMusicAnalyzer analyzer,
        List<AiProcessingWorkItem> workItems,
        AiMusicOptions options,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var profilePaths = workItems.Select(w => w.SongPath).ToArray();
        var profiles = await dbContext.AiTrackProfiles
            .Where(p => profilePaths.Contains(p.SongPath))
            .ToDictionaryAsync(p => p.SongPath, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var genres = await dbContext.AiGenreDefinitions.AsNoTracking().Where(g => g.IsActive).ToListAsync(cancellationToken);
        var songCandidates = workItems
            .Select(item => library.Songs.FirstOrDefault(s => s.Path == item.SongPath))
            .Where(song => song != null)
            .Select(song => new AiSongCandidate(
                song!.Path!,
                song.Name,
                song.Artist ?? string.Empty,
                song.Album ?? string.Empty,
                buildFolderHint(song.Path)))
            .ToList();
        var songCandidatesByPath = songCandidates.ToDictionary(candidate => candidate.SongPath, StringComparer.OrdinalIgnoreCase);
        var updatedSongs = new List<string>();

        IReadOnlyList<AiTrackAnalysisResult> results;
        try
        {
            results = await analyzer.AnalyzeBatchAsync(songCandidates, genres, cancellationToken);
        }
        catch (AiBatchAnalysisException ex) when (ex.ShouldRetryIndividually && workItems.Count > 1)
        {
            logger.LogWarning(
                ex,
                "Batch {BatchId} returned truncated AI JSON near {Path}; retrying {Count} song(s) individually",
                workItems[0].BatchId ?? "batch",
                ex.JsonPath,
                workItems.Count);

            foreach (var item in workItems)
            {
                if (!songCandidatesByPath.TryGetValue(item.SongPath, out var candidate))
                {
                    markItemFailed(item, "AI fallback skipped because song metadata was unavailable.");
                    continue;
                }

                try
                {
                    var singleResults = await analyzer.AnalyzeBatchAsync([candidate], genres, cancellationToken);
                    var singleResult = singleResults.FirstOrDefault(result =>
                        string.Equals(result.SongPath, item.SongPath, StringComparison.OrdinalIgnoreCase));

                    if (singleResult == null)
                    {
                        markItemFailed(item, $"AI fallback response missing result after batch truncation near {ex.JsonPath}.");
                        continue;
                    }

                    await applyResultAsync(item, singleResult);
                }
                catch (Exception fallbackEx)
                {
                    logger.LogError(
                        fallbackEx,
                        "Per-song AI fallback failed for {SongPath} after batch truncation near {Path}",
                        item.SongPath,
                        ex.JsonPath);
                    markItemFailed(item, $"Batch JSON truncated near {ex.JsonPath}; per-song fallback failed: {fallbackEx.Message}");
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (updatedSongs.Count > 0)
            {
                await updateSimilarityAsync(dbContext, updatedSongs, cancellationToken);
            }

            return;
        }
        catch (Exception ex)
        {
            foreach (var item in workItems)
            {
                markItemFailed(item, ex.Message);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var resultLookup = results.ToDictionary(r => r.SongPath, StringComparer.OrdinalIgnoreCase);

        foreach (var item in workItems)
        {
            if (!resultLookup.TryGetValue(item.SongPath, out var result))
            {
                markItemFailed(item, "AI response missing result.");
                continue;
            }

            await applyResultAsync(item, result);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updatedSongs.Count > 0)
        {
            await updateSimilarityAsync(dbContext, updatedSongs, cancellationToken);
        }

        AiTrackProfile getOrCreateProfile(AiProcessingWorkItem item)
        {
            if (profiles.TryGetValue(item.SongPath, out var existingProfile))
            {
                return existingProfile;
            }

            var profile = new AiTrackProfile { SongPath = item.SongPath };
            dbContext.AiTrackProfiles.Add(profile);
            profiles[item.SongPath] = profile;
            return profile;
        }

        async Task applyResultAsync(AiProcessingWorkItem item, AiTrackAnalysisResult result)
        {
            var profile = getOrCreateProfile(item);

            profile.Fingerprint = item.Fingerprint;
            profile.AnalysisVersion = options.AnalysisVersion;
            profile.Status = AiProcessingStatus.Completed;
            profile.Attempts = item.Attempts;
            profile.LastError = null;
            profile.LastAnalyzedUtc = now;
            profile.Summary = result.Summary;
            profile.TempoLabel = result.TempoLabel;
            profile.PrimaryMood = result.PrimaryMood;
            profile.Energy = clamp(result.Energy);
            profile.Acousticness = clamp(result.Acousticness);
            profile.Instrumentalness = clamp(result.Instrumentalness);
            profile.VocalPresence = clamp(result.VocalPresence);
            profile.Sacredness = clamp(result.Sacredness);
            profile.SeasonalityChristmas = clamp(result.SeasonalityChristmas);
            profile.Danceability = clamp(result.Danceability);
            profile.Warmth = clamp(result.Warmth);
            profile.Confidence = clamp(result.Confidence);

            var markers = await dbContext.AiTrackMarkers.Where(m => m.SongPath == item.SongPath).ToListAsync(cancellationToken);
            dbContext.AiTrackMarkers.RemoveRange(markers);
            dbContext.AiTrackMarkers.AddRange(result.Markers.Select(m => new AiTrackMarker
            {
                SongPath = item.SongPath,
                MarkerKey = m.Key,
                MarkerValue = clamp(m.Value),
                Confidence = clamp(m.Confidence)
            }));

            var scores = await dbContext.AiTrackGenreScores.Where(g => g.SongPath == item.SongPath).ToListAsync(cancellationToken);
            dbContext.AiTrackGenreScores.RemoveRange(scores);
            dbContext.AiTrackGenreScores.AddRange(result.Genres.Select(g => new AiTrackGenreScore
            {
                SongPath = item.SongPath,
                GenreKey = g.GenreKey,
                Score = clamp(g.Score),
                Rank = g.Rank,
                Why = g.Why
            }));

            item.Status = AiProcessingStatus.Completed;
            item.LastError = null;
            item.CompletedUtc = now;
            updatedSongs.Add(item.SongPath);
        }

        void markItemFailed(AiProcessingWorkItem item, string error)
        {
            item.Status = AiProcessingStatus.Failed;
            item.LastError = error;
            item.CompletedUtc = now;

            var profile = getOrCreateProfile(item);
            profile.Fingerprint = item.Fingerprint;
            profile.AnalysisVersion = options.AnalysisVersion;
            profile.Status = AiProcessingStatus.Failed;
            profile.Attempts = item.Attempts;
            profile.LastError = error;
        }
    }

    private async Task updateSimilarityAsync(
        MusicContext dbContext,
        IReadOnlyList<string> updatedSongPaths,
        CancellationToken cancellationToken)
    {
        var profiles = await dbContext.AiTrackProfiles.AsNoTracking()
            .Where(p => p.Status == AiProcessingStatus.Completed)
            .ToListAsync(cancellationToken);

        var markers = await dbContext.AiTrackMarkers.AsNoTracking().ToListAsync(cancellationToken);
        var genres = await dbContext.AiTrackGenreScores.AsNoTracking().ToListAsync(cancellationToken);

        var markerLookup = markers.GroupBy(m => m.SongPath)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var genreLookup = genres.GroupBy(g => g.SongPath)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var songPath in updatedSongPaths)
        {
            var sourceProfile = profiles.FirstOrDefault(p => p.SongPath == songPath);
            if (sourceProfile == null)
            {
                continue;
            }

            var sourceVector = buildVector(sourceProfile, markerLookup.GetValueOrDefault(songPath), genreLookup.GetValueOrDefault(songPath));
            if (sourceVector.Count == 0)
            {
                continue;
            }

            var similarities = new List<AiTrackSimilarity>();
            foreach (var other in profiles)
            {
                if (other.SongPath == songPath)
                {
                    continue;
                }

                var otherVector = buildVector(other, markerLookup.GetValueOrDefault(other.SongPath), genreLookup.GetValueOrDefault(other.SongPath));
                var score = computeCosineSimilarity(sourceVector, otherVector);
                if (score <= 0)
                {
                    continue;
                }

                similarities.Add(new AiTrackSimilarity
                {
                    SongPath = songPath,
                    SimilarSongPath = other.SongPath,
                    Score = score,
                    ReasonsJson = JsonSerializer.Serialize(new { score }),
                    UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime
                });
            }

            var top = similarities.OrderByDescending(s => s.Score).Take(SimilarityNeighbors).ToList();
            var existing = await dbContext.AiTrackSimilarities.Where(s => s.SongPath == songPath).ToListAsync(cancellationToken);
            dbContext.AiTrackSimilarities.RemoveRange(existing);
            dbContext.AiTrackSimilarities.AddRange(top);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static Dictionary<string, double> buildVector(
        AiTrackProfile profile,
        List<AiTrackMarker>? markers,
        List<AiTrackGenreScore>? genres)
    {
        var vector = new Dictionary<string, double>
        {
            ["energy"] = profile.Energy,
            ["acousticness"] = profile.Acousticness,
            ["instrumentalness"] = profile.Instrumentalness,
            ["vocalPresence"] = profile.VocalPresence,
            ["sacredness"] = profile.Sacredness,
            ["seasonalityChristmas"] = profile.SeasonalityChristmas,
            ["danceability"] = profile.Danceability,
            ["warmth"] = profile.Warmth
        };

        if (markers != null)
        {
            foreach (var marker in markers)
            {
                vector[$"marker:{marker.MarkerKey}"] = marker.MarkerValue;
            }
        }

        if (genres != null)
        {
            foreach (var genre in genres)
            {
                vector[$"genre:{genre.GenreKey}"] = genre.Score;
            }
        }

        return vector;
    }

    private static double computeCosineSimilarity(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;

        foreach (var kvp in a)
        {
            normA += kvp.Value * kvp.Value;
            if (b.TryGetValue(kvp.Key, out var bValue))
            {
                dot += kvp.Value * bValue;
            }
        }

        foreach (var kvp in b)
        {
            normB += kvp.Value * kvp.Value;
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static double clamp(double value) => Math.Max(0, Math.Min(1, value));

    private static string computeFingerprint(Song song)
    {
        var path = song.Path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (!File.Exists(path))
        {
            return $"{path}|missing";
        }

        var info = new FileInfo(path);
        return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{song.Name}|{song.Artist}|{song.Album}";
    }

    private static string? buildFolderHint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? null : Path.GetFileName(directory);
    }
}
