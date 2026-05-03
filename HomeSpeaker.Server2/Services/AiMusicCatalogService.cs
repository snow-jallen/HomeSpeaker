using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class AiMusicCatalogService
{
    private readonly MusicContext dbContext;
    private readonly Mp3Library library;
    private readonly AiMusicOptions options;

    public AiMusicCatalogService(MusicContext dbContext, Mp3Library library, IOptions<AiMusicOptions> options)
    {
        this.dbContext = dbContext;
        this.library = library;
        this.options = options.Value;
    }

    public async Task<AiLibraryStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var run = await dbContext.AiProcessingRuns.AsNoTracking()
            .OrderByDescending(r => r.LastHeartbeatUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var totalTracks = library.Songs.Count(s => !string.IsNullOrWhiteSpace(s.Path));
        var queued = await dbContext.AiProcessingWorkItems.CountAsync(w => w.Status == AiProcessingStatus.Pending, cancellationToken);
        var processing = await dbContext.AiProcessingWorkItems.CountAsync(w => w.Status == AiProcessingStatus.Processing, cancellationToken);
        var completed = await dbContext.AiTrackProfiles.CountAsync(p => p.Status == AiProcessingStatus.Completed, cancellationToken);
        var failed = await dbContext.AiTrackProfiles.CountAsync(p => p.Status == AiProcessingStatus.Failed, cancellationToken);
        var percentComplete = totalTracks == 0 ? 0 : (double)completed / totalTracks * 100;
        var lastFailure = await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => !string.IsNullOrWhiteSpace(w.LastError) && (w.CompletedUtc != null || w.StartedUtc != null))
            .OrderByDescending(w => w.CompletedUtc ?? w.StartedUtc ?? w.QueuedUtc)
            .Select(w => new
            {
                w.LastError,
                OccurredUtc = w.CompletedUtc ?? w.StartedUtc ?? w.QueuedUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
        var lastFailureMessage = sanitizeStatusMessage(lastFailure?.LastError);
        var profileErrors = (await dbContext.AiTrackProfiles.AsNoTracking()
            .Where(p => p.Status == AiProcessingStatus.Failed && !string.IsNullOrWhiteSpace(p.LastError))
            .OrderByDescending(p => p.LastAnalyzedUtc)
            .Select(p => p.LastError)
            .Take(10)
            .ToListAsync(cancellationToken))
            .Select(error => sanitizeStatusMessage(error))
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Cast<string>();
        var workItemErrors = (await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => !string.IsNullOrWhiteSpace(w.LastError))
            .OrderByDescending(w => w.CompletedUtc ?? w.StartedUtc ?? w.QueuedUtc)
            .Select(w => w.LastError)
            .Take(10)
            .ToListAsync(cancellationToken))
            .Select(error => sanitizeStatusMessage(error))
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Cast<string>();
        var errorDetails = profileErrors
            .Concat(workItemErrors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var degradedReason = buildDegradedReason(run?.State, lastFailureMessage);
        var recentActivity = await getRecentActivityAsync(
            run,
            totalTracks,
            queued,
            processing,
            completed,
            failed,
            cancellationToken);

        return new AiLibraryStatusDto
        {
            State = run?.State.ToString() ?? "Idle",
            TotalTracks = totalTracks,
            QueuedTracks = queued,
            ProcessingTracks = processing,
            CompletedTracks = completed,
            FailedTracks = failed,
            PercentComplete = percentComplete,
            LastScanUtc = run?.LastScanUtc,
            LastHeartbeatUtc = run?.LastHeartbeatUtc,
            CurrentBatchId = run?.CurrentBatchId,
            DegradedReason = degradedReason,
            LastErrorMessage = lastFailureMessage,
            LastFailureUtc = lastFailure?.OccurredUtc,
            ErrorDetails = errorDetails,
            RecentActivity = recentActivity
        };
    }

    private async Task<IReadOnlyList<AiStatusActivityDto>> getRecentActivityAsync(
        AiProcessingRun? run,
        int totalTracks,
        int queued,
        int processing,
        int completed,
        int failed,
        CancellationToken cancellationToken)
    {
        var activities = new List<AiStatusActivityDto>();

        if (run?.LastHeartbeatUtc is DateTime heartbeatUtc &&
            (totalTracks > 0 || queued > 0 || processing > 0 || completed > 0 || failed > 0))
        {
            activities.Add(new AiStatusActivityDto
            {
                TimestampUtc = heartbeatUtc,
                Kind = run.State switch
                {
                    AiProcessingState.Scanning => "scan-progress",
                    AiProcessingState.Processing => "progress",
                    AiProcessingState.Degraded => "degraded",
                    _ => "status"
                },
                Message = buildProgressMessage(run.State, run.CurrentBatchId, totalTracks, queued, processing, completed, failed),
                BatchId = run.CurrentBatchId
            });
        }

        if (run?.LastScanUtc is DateTime lastScanUtc)
        {
            activities.Add(new AiStatusActivityDto
            {
                TimestampUtc = lastScanUtc,
                Kind = "scan-completed",
                Message = $"Library scan completed. {queued} queued, {processing} processing, {completed} completed, {failed} failed."
            });
        }

        var recentBatchRows = await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => w.BatchId != null && (w.StartedUtc != null || w.CompletedUtc != null))
            .OrderByDescending(w => w.CompletedUtc ?? w.StartedUtc ?? w.QueuedUtc)
            .Select(w => new BatchActivityRow(
                w.BatchId!,
                w.Status,
                w.Attempts,
                w.StartedUtc,
                w.CompletedUtc,
                w.QueuedUtc))
            .Take(120)
            .ToListAsync(cancellationToken);

        activities.AddRange(recentBatchRows
            .GroupBy(row => row.BatchId, StringComparer.OrdinalIgnoreCase)
            .Select(buildBatchActivity)
            .OfType<AiStatusActivityDto>());

        var songsByPath = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .GroupBy(s => s.Path!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var recentFailures = await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => w.CompletedUtc != null && !string.IsNullOrWhiteSpace(w.LastError))
            .OrderByDescending(w => w.CompletedUtc)
            .Select(w => new ItemActivityRow(
                w.SongPath,
                w.Attempts,
                w.CompletedUtc ?? w.StartedUtc ?? w.QueuedUtc,
                w.LastError,
                w.BatchId))
            .Take(5)
            .ToListAsync(cancellationToken);

        activities.AddRange(recentFailures.Select(failure => new AiStatusActivityDto
        {
            TimestampUtc = failure.TimestampUtc,
            Kind = "track-failed",
            Message = $"{buildSongLabel(failure.SongPath, songsByPath)} failed on attempt {Math.Max(1, failure.Attempts)}: {sanitizeStatusMessage(failure.Message, 160) ?? "Unknown error."}",
            BatchId = failure.BatchId
        }));

        var recentRetries = await dbContext.AiProcessingWorkItems.AsNoTracking()
            .Where(w => w.Status == AiProcessingStatus.Processing && w.Attempts > 1 && w.StartedUtc != null)
            .OrderByDescending(w => w.StartedUtc)
            .Select(w => new ItemActivityRow(
                w.SongPath,
                w.Attempts,
                w.StartedUtc ?? w.QueuedUtc,
                null,
                w.BatchId))
            .Take(5)
            .ToListAsync(cancellationToken);

        activities.AddRange(recentRetries.Select(retry => new AiStatusActivityDto
        {
            TimestampUtc = retry.TimestampUtc,
            Kind = "retry",
            Message = $"Retrying {buildSongLabel(retry.SongPath, songsByPath)} (attempt {retry.Attempts}).",
            BatchId = retry.BatchId
        }));

        return activities
            .OrderByDescending(activity => activity.TimestampUtc)
            .Take(20)
            .ToArray();
    }

    private string? buildDegradedReason(AiProcessingState? state, string? lastFailureMessage)
    {
        if (state != AiProcessingState.Degraded)
        {
            return null;
        }

        if (!options.HasConfiguredProvider)
        {
            return options.ConfigurationIssue;
        }

        return lastFailureMessage;
    }

    private static string buildProgressMessage(
        AiProcessingState state,
        string? currentBatchId,
        int totalTracks,
        int queued,
        int processing,
        int completed,
        int failed)
    {
        var processedSummary = totalTracks > 0
            ? $"{completed} of {totalTracks} analyzed"
            : $"{completed} analyzed";

        return state switch
        {
            AiProcessingState.Scanning => $"Scanning library. {processedSummary}, {queued} queued, {failed} failed.",
            AiProcessingState.Processing => $"Processing {formatBatchLabel(currentBatchId)}. {processedSummary}, {processing} active, {queued} queued, {failed} failed.",
            AiProcessingState.Degraded => $"Processing degraded. {processedSummary}, {queued} queued, {failed} failed.",
            _ when queued > 0 => $"Processing paused with {queued} queued. {processedSummary}, {failed} failed.",
            _ => $"Processing idle. {processedSummary}, {failed} failed."
        };
    }

    private static AiStatusActivityDto? buildBatchActivity(IGrouping<string, BatchActivityRow> group)
    {
        var items = group.ToList();
        if (items.Count == 0)
        {
            return null;
        }

        var total = items.Count;
        var processingCount = items.Count(item => item.Status == AiProcessingStatus.Processing);
        var completedCount = items.Count(item => item.Status == AiProcessingStatus.Completed);
        var failedCount = items.Count(item => item.Status == AiProcessingStatus.Failed);
        var retryCount = items.Count(item => item.Attempts > 1);
        var batchLabel = formatBatchLabel(group.Key);
        var retrySummary = retryCount > 0 ? $" {retryCount} retried." : string.Empty;

        if (processingCount > 0)
        {
            var startedUtc = items
                .Where(item => item.StartedUtc.HasValue)
                .Select(item => item.StartedUtc!.Value)
                .DefaultIfEmpty(items.Max(item => item.QueuedUtc))
                .Min();

            return new AiStatusActivityDto
            {
                TimestampUtc = startedUtc,
                Kind = "batch-started",
                Message = $"{batchLabel} started for {total} track{(total == 1 ? string.Empty : "s")}.{retrySummary}",
                BatchId = group.Key
            };
        }

        var completedUtc = items
            .Select(item => item.CompletedUtc ?? item.StartedUtc ?? item.QueuedUtc)
            .Max();

        if (failedCount > 0)
        {
            return new AiStatusActivityDto
            {
                TimestampUtc = completedUtc,
                Kind = "batch-failed",
                Message = $"{batchLabel} finished with {completedCount} completed and {failedCount} failed.{retrySummary}",
                BatchId = group.Key
            };
        }

        return new AiStatusActivityDto
        {
            TimestampUtc = completedUtc,
            Kind = "batch-completed",
            Message = $"{batchLabel} completed {completedCount} track{(completedCount == 1 ? string.Empty : "s")}.{retrySummary}",
            BatchId = group.Key
        };
    }

    private static string buildSongLabel(string songPath, IReadOnlyDictionary<string, Song> songsByPath)
    {
        if (songsByPath.TryGetValue(songPath, out var song))
        {
            if (!string.IsNullOrWhiteSpace(song.Artist))
            {
                return $"{song.Artist} — {song.Name}";
            }

            if (!string.IsNullOrWhiteSpace(song.Name))
            {
                return song.Name;
            }
        }

        return Path.GetFileNameWithoutExtension(songPath);
    }

    private static string formatBatchLabel(string? batchId)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return "batch";
        }

        return $"batch {batchId[..Math.Min(8, batchId.Length)]}";
    }

    private static string? sanitizeStatusMessage(string? message, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        if (maxLength is > 0 && trimmed.Length > maxLength.Value)
        {
            return $"{trimmed[..(maxLength.Value - 1)]}…";
        }

        return trimmed;
    }

    public async Task<IReadOnlyList<AiPlaylistSummaryDto>> GetGenreSummariesAsync(CancellationToken cancellationToken)
    {
        var genres = await dbContext.AiGenreDefinitions.AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);

        var scoreGroups = await dbContext.AiTrackGenreScores.AsNoTracking()
            .GroupBy(g => g.GenreKey)
            .Select(g => new { GenreKey = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var lastUpdatedGroups = await dbContext.AiTrackGenreScores.AsNoTracking()
            .Join(
                dbContext.AiTrackProfiles.AsNoTracking()
                    .Where(profile => profile.Status == AiProcessingStatus.Completed && profile.LastAnalyzedUtc != null),
                score => score.SongPath,
                profile => profile.SongPath,
                (score, profile) => new
                {
                    score.GenreKey,
                    profile.LastAnalyzedUtc
                })
            .GroupBy(item => item.GenreKey)
            .Select(group => new
            {
                GenreKey = group.Key,
                LastUpdatedUtc = group.Max(item => item.LastAnalyzedUtc)
            })
            .ToListAsync(cancellationToken);

        var scoreCounts = scoreGroups.ToDictionary(x => x.GenreKey, x => x.Count, StringComparer.OrdinalIgnoreCase);
        var lastUpdatedByGenre = lastUpdatedGroups.ToDictionary(x => x.GenreKey, x => x.LastUpdatedUtc, StringComparer.OrdinalIgnoreCase);
        var summaries = new List<AiPlaylistSummaryDto>();
        foreach (var genre in genres)
        {
            summaries.Add(new AiPlaylistSummaryDto
            {
                GenreKey = genre.Key,
                DisplayName = genre.DisplayName,
                Description = genre.Description,
                TrackCount = scoreCounts.GetValueOrDefault(genre.Key),
                SortOrder = genre.SortOrder,
                LastUpdatedUtc = lastUpdatedByGenre.GetValueOrDefault(genre.Key)
            });
        }

        return summaries;
    }

    public async Task<AiPlaylistDto?> GetGenrePlaylistAsync(string genreKey, int maxTracks, CancellationToken cancellationToken)
    {
        var genre = await dbContext.AiGenreDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Key == genreKey && g.IsActive, cancellationToken);
        if (genre == null)
        {
            return null;
        }

        var scores = await dbContext.AiTrackGenreScores.AsNoTracking()
            .Where(s => s.GenreKey == genreKey)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Rank)
            .Take(maxTracks)
            .ToListAsync(cancellationToken);

        var songPaths = scores
            .Select(score => score.SongPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var markers = await dbContext.AiTrackMarkers.AsNoTracking()
            .Where(marker => songPaths.Contains(marker.SongPath))
            .OrderByDescending(marker => marker.MarkerValue)
            .ThenByDescending(marker => marker.Confidence)
            .ToListAsync(cancellationToken);

        var songsByPath = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToDictionary(s => s.Path!, s => s, StringComparer.OrdinalIgnoreCase);

        var markersBySongPath = markers
            .GroupBy(marker => marker.SongPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AiPlaylistTrackMarkerDto>)group
                    .Select(marker => new AiPlaylistTrackMarkerDto
                    {
                        Key = marker.MarkerKey,
                        Value = marker.MarkerValue,
                        Confidence = marker.Confidence
                    })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var tracks = scores
            .Select(score =>
            {
                if (!songsByPath.TryGetValue(score.SongPath, out var song))
                {
                    return null;
                }

                return new AiPlaylistTrackDto
                {
                    Song = song,
                    GenreScore = score.Score,
                    GenreRank = score.Rank,
                    Why = string.IsNullOrWhiteSpace(score.Why) ? null : score.Why.Trim(),
                    Markers = markersBySongPath.GetValueOrDefault(score.SongPath, Array.Empty<AiPlaylistTrackMarkerDto>())
                };
            })
            .OfType<AiPlaylistTrackDto>()
            .ToList();

        return new AiPlaylistDto
        {
            GenreKey = genre.Key,
            DisplayName = genre.DisplayName,
            Description = genre.Description,
            Tracks = tracks,
            Songs = tracks.Select(track => track.Song).ToList()
        };
    }

    public async Task<IReadOnlyList<Song>> GetSimilarSongsAsync(string songPath, int maxTracks, CancellationToken cancellationToken)
    {
        var similarities = await dbContext.AiTrackSimilarities.AsNoTracking()
            .Where(s => s.SongPath == songPath)
            .OrderByDescending(s => s.Score)
            .Take(maxTracks)
            .ToListAsync(cancellationToken);

        var songsByPath = library.Songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Path))
            .ToDictionary(s => s.Path!, s => s);

        return similarities
            .Select(sim => songsByPath.GetValueOrDefault(sim.SimilarSongPath))
            .OfType<Song>()
            .ToList();
    }

    private sealed record BatchActivityRow(
        string BatchId,
        AiProcessingStatus Status,
        int Attempts,
        DateTime? StartedUtc,
        DateTime? CompletedUtc,
        DateTime QueuedUtc);

    private sealed record ItemActivityRow(
        string SongPath,
        int Attempts,
        DateTime TimestampUtc,
        string? Message,
        string? BatchId);
}
