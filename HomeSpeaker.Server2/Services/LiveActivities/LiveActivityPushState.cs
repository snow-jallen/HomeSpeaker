namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>What has been pushed to one Live Activity, used to decide what to push next.</summary>
public sealed class LiveActivityPushState
{
    /// <summary>The status last delivered to the activity, or null before the first push.</summary>
    public PlayerStatusSnapshot? LastSent { get; set; }

    /// <summary>When the last priority-5 push went out, for throttling.</summary>
    public DateTimeOffset? LastLowPrioritySentAt { get; set; }

    /// <summary>Since when playback has been continuously stopped, or null while playing.</summary>
    public DateTimeOffset? StoppedSince { get; set; }

    /// <summary>After a transient APNs failure, no retry happens before this time.</summary>
    public DateTimeOffset? RetryNotBefore { get; set; }
}
