using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

/// <summary>A <see cref="PlayerStatusDto"/> plus the moment it was read.</summary>
public sealed record PlayerStatusSnapshot(PlayerStatusDto Status, DateTimeOffset CapturedAt)
{
    /// <summary>When the sleep timer will stop playback, or null when no timer is running.</summary>
    public DateTimeOffset? SleepTimerEndsAt =>
        Status.SleepTimerActive && Status.SleepTimerRemainingMinutes is { } minutes
            ? CapturedAt + TimeSpan.FromMinutes(minutes)
            : null;
}
