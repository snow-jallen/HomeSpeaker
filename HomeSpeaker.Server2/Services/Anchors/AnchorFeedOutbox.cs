using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Ticks on an external feed's anchors, kept until the feed takes them. A tick shows as pending meanwhile; a refused one
/// is dropped (the board then shows what the feed says); an unreachable feed is retried with a growing wait.
/// </summary>
public static class AnchorFeedOutbox
{
    private static readonly TimeSpan firstWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan longestWait = TimeSpan.FromMinutes(5);

    /// <summary>10 s after the first failure, doubling, at most 5 minutes.</summary>
    public static TimeSpan Backoff(int attempts)
    {
        var seconds = firstWait.TotalSeconds * Math.Pow(2, Math.Clamp(attempts - 1, 0, 20));
        return TimeSpan.FromSeconds(Math.Min(seconds, longestWait.TotalSeconds));
    }

    public static async Task EnqueueAsync(MusicContext db, string feedAnchorId, DateOnly date, bool done, DateTimeOffset clientUpdatedAt, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var entry = await db.AnchorFeedOutbox.FirstOrDefaultAsync(e => e.FeedAnchorId == feedAnchorId && e.Date == date, cancellationToken);
        if (entry is null)
        {
            entry = new AnchorFeedOutboxEntity { FeedAnchorId = feedAnchorId, Date = date, CreatedAt = now };
            db.AnchorFeedOutbox.Add(entry);
        }

        entry.Done = done;
        entry.ClientUpdatedAt = clientUpdatedAt;
        entry.Attempts = 0;
        entry.LastError = null;
        entry.NextAttemptAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>What's still waiting, keyed by the board's anchor id (<c>feed:...</c>) and date.</summary>
    public static async Task<IReadOnlyDictionary<(string AnchorId, DateOnly Date), bool>> PendingAsync(MusicContext db, CancellationToken cancellationToken)
    {
        var entries = await db.AnchorFeedOutbox.AsNoTracking().ToListAsync(cancellationToken);
        return entries.ToDictionary(e => (FeedAnchorMerge.WireId(e.FeedAnchorId), e.Date), e => e.Done);
    }

    /// <summary>
    /// Sends every entry that is due; returns how many left the outbox (taken or refused). Each row is settled only if it
    /// still holds what was sent: a newer tick queued meanwhile (an Undo, say) stays queued and goes next.
    /// </summary>
    public static async Task<int> SendDueAsync(MusicContext db, HttpAnchorFeed feed, TimeProvider timeProvider, CancellationToken cancellationToken, ILogger? logger = null)
    {
        var now = timeProvider.GetUtcNow();

        // Few rows, and SQLite can't compare DateTimeOffset columns in SQL, so filter here.
        var due = (await db.AnchorFeedOutbox.AsNoTracking().ToListAsync(cancellationToken))
            .Where(e => e.NextAttemptAt <= now)
            .OrderBy(e => e.CreatedAt)
            .ToList();
        var finished = 0;
        foreach (var sent in due)
        {
            var outcome = await feed.WriteAsync(sent.FeedAnchorId, sent.Date, sent.Done, sent.ClientUpdatedAt, cancellationToken);
            db.ChangeTracker.Clear();
            var current = await db.AnchorFeedOutbox.FirstOrDefaultAsync(e => e.Id == sent.Id, cancellationToken);
            if (current is null || current.Done != sent.Done || current.ClientUpdatedAt != sent.ClientUpdatedAt)
            {
                // Replaced while it was on its way: leave the newer tick alone.
                continue;
            }

            if (outcome == FeedWriteOutcome.Failed)
            {
                current.Attempts++;
                current.LastError = "The feed couldn't be reached.";
                current.NextAttemptAt = now + Backoff(current.Attempts);
            }
            else
            {
                if (outcome == FeedWriteOutcome.Rejected)
                {
                    logger?.LogWarning("Anchor feed refused a tick for {AnchorId} on {Date}; dropping it", sent.FeedAnchorId, sent.Date);
                }

                db.AnchorFeedOutbox.Remove(current);
                finished++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        if (finished > 0)
        {
            feed.Expire();
        }

        return finished;
    }
}
