using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>
/// Turns a stream of player status samples and skip presses into finished plays.
/// A song counts as played once it ran for <see cref="MinimumListen"/> (or half of a shorter
/// song), and as skipped when someone pressed skip before half of it played. A song that was
/// stopped or replaced early without a skip is just dropped: that says nothing about the song.
/// Not thread-safe; <see cref="PlayHistoryRecorder"/> serializes calls.
/// </summary>
public sealed class PlayTracker
{
    public static readonly TimeSpan MinimumListen = TimeSpan.FromSeconds(30);

    /// <summary>How long nothing may play before the last song counts as finished.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(10);

    // A sample this far behind the furthest point already heard means the song started over.
    private static readonly TimeSpan restartJump = TimeSpan.FromSeconds(10);

    private Current? current;

    // After a skip the status can still show the skipped song for a moment; don't count it again.
    private string? justSkippedPath;

    /// <summary>Feeds one status sample; returns the previous song if this sample shows it ended.</summary>
    public CompletedPlay? Observe(PlayerStatus status, DateTimeOffset now)
    {
        var path = playingSongPath(status);
        if (path is not null && path == justSkippedPath)
        {
            return null;
        }

        if (path is not null)
        {
            justSkippedPath = null;
        }

        if (current is null)
        {
            current = path is null ? null : new Current(path, now, status.Elapsed, lengthOf(status), IdleSince: null);
            return null;
        }

        if (path is null)
        {
            // Paused or stopped. Wait to see whether the same song resumes before calling it finished.
            var idleSince = current.IdleSince ?? now;
            if (now - idleSince < IdleLimit)
            {
                current = current with { IdleSince = idleSince };
                return null;
            }

            var ended = finish(current, skipped: false);
            current = null;
            return ended;
        }

        if (path == current.Path && status.Elapsed + restartJump >= current.Heard)
        {
            current = current with
            {
                Heard = status.Elapsed > current.Heard ? status.Elapsed : current.Heard,
                Length = lengthOf(status) ?? current.Length,
                IdleSince = null,
            };
            return null;
        }

        var finished = finish(current, skipped: false);
        current = new Current(path, now, status.Elapsed, lengthOf(status), IdleSince: null);
        return finished;
    }

    /// <summary>Records a skip press; returns the song that was playing.</summary>
    public CompletedPlay? Skipped()
    {
        if (current is null)
        {
            return null;
        }

        var skippedEarly = current.Length is { } length
            ? current.Heard < length / 2
            : current.Heard < MinimumListen * 2;
        var finished = finish(current, skippedEarly);
        justSkippedPath = current.Path;
        current = null;
        return finished;
    }

    private static CompletedPlay? finish(Current song, bool skipped)
    {
        if (skipped)
        {
            return new CompletedPlay(song.Path, song.StartedAt, song.Heard, Skipped: true);
        }

        var enough = song.Heard >= MinimumListen || (song.Length is { } length && song.Heard >= length / 2);
        return enough ? new CompletedPlay(song.Path, song.StartedAt, song.Heard, Skipped: false) : null;
    }

    private static string? playingSongPath(PlayerStatus status) =>
        status.StillPlaying && !status.IsStream && !string.IsNullOrEmpty(status.CurrentSong?.Path)
            ? status.CurrentSong.Path
            : null;

    private static TimeSpan? lengthOf(PlayerStatus status)
    {
        var length = status.Elapsed + status.Remaining;
        return length > TimeSpan.Zero && status.Remaining > TimeSpan.Zero ? length : null;
    }

    private sealed record Current(string Path, DateTimeOffset StartedAt, TimeSpan Heard, TimeSpan? Length, DateTimeOffset? IdleSince);
}
