namespace HomeSpeaker.Server2.Services;

/// <summary>Which parts of the player status changed between two snapshots.</summary>
public sealed record PlayerStatusChange(
    bool SongChanged,
    bool TitleChanged,
    bool PlaybackChanged,
    bool StreamChanged,
    bool VolumeChanged,
    bool RepeatModeChanged,
    bool SleepTimerChanged)
{
    /// <summary>Everything is new: there was no previous snapshot to compare against.</summary>
    public static PlayerStatusChange Initial { get; } = new(true, true, true, true, true, true, true);

    public static PlayerStatusChange None { get; } = new(false, false, false, false, false, false, false);

    public bool IsMaterial =>
        SongChanged || TitleChanged || PlaybackChanged || StreamChanged ||
        VolumeChanged || RepeatModeChanged || SleepTimerChanged;

    /// <summary>
    /// Changes the listener notices straight away (a different song, play/stop, radio vs. library),
    /// which justify an immediate (priority 10) Live Activity push.
    /// </summary>
    public bool IsHighPriority => SongChanged || PlaybackChanged || StreamChanged;
}
