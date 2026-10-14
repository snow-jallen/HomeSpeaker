using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Decides whether a player status changed in a way listeners care about. Elapsed/remaining time
/// ticking along is deliberately not a change.
/// </summary>
public static class PlayerStatusChangeDetector
{
    /// <summary>How far the sleep timer's end time may drift before it counts as changed.</summary>
    public static readonly TimeSpan SleepTimerTolerance = TimeSpan.FromSeconds(30);

    public static PlayerStatusChange Compare(PlayerStatusSnapshot? previous, PlayerStatusSnapshot current)
    {
        if (previous is null)
        {
            return PlayerStatusChange.Initial;
        }

        var before = previous.Status;
        var after = current.Status;
        var sameSong = isSameSong(before.CurrentSong, after.CurrentSong);

        // Same song but a new name: a radio stream's ICY "now playing" title moved on.
        var titleChanged = sameSong && !string.Equals(before.CurrentSong?.Name, after.CurrentSong?.Name, StringComparison.Ordinal);

        return new PlayerStatusChange(
            SongChanged: !sameSong,
            TitleChanged: titleChanged,
            PlaybackChanged: before.StillPlaying != after.StillPlaying,
            StreamChanged: before.IsStream != after.IsStream || !string.Equals(before.StreamName, after.StreamName, StringComparison.Ordinal),
            VolumeChanged: before.Volume != after.Volume,
            RepeatModeChanged: before.RepeatMode != after.RepeatMode,
            SleepTimerChanged: before.SleepTimerActive != after.SleepTimerActive ||
                sleepTimerEndMoved(previous.SleepTimerEndsAt, current.SleepTimerEndsAt));
    }

    private static bool isSameSong(Song? before, Song? after)
    {
        if (before is null || after is null)
        {
            return before is null && after is null;
        }

        return before.SongId == after.SongId &&
               string.Equals(before.Path, after.Path, StringComparison.Ordinal);
    }

    private static bool sleepTimerEndMoved(DateTimeOffset? before, DateTimeOffset? after)
    {
        if (before is null || after is null)
        {
            return before.HasValue != after.HasValue;
        }

        return (after.Value - before.Value).Duration() > SleepTimerTolerance;
    }
}
