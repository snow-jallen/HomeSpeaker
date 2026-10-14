namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// Decides whether a Live Activity needs a push for the current status. Comparing against what
/// was last <em>sent</em> (rather than the previous status) makes coalescing automatic: a change
/// held back by the throttle is still a difference when the window reopens, so the newest state
/// goes out then.
/// </summary>
public static class LiveActivityPushPlanner
{
    /// <summary>At most one priority-5 push per activity in this window.</summary>
    public static readonly TimeSpan LowPriorityInterval = TimeSpan.FromSeconds(10);

    /// <summary>Playback stopped this long ends the activity.</summary>
    public static readonly TimeSpan EndAfterStopped = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Plans the next push, or returns null when nothing needs sending. Also records when
    /// playback stopped, so call it for every status refresh.
    /// </summary>
    public static LiveActivityPushPlan? Plan(LiveActivityPushState state, PlayerStatusSnapshot current, DateTimeOffset now)
    {
        if (current.Status.StillPlaying)
        {
            state.StoppedSince = null;
        }
        else
        {
            state.StoppedSince ??= now;
        }

        if (state.RetryNotBefore is { } retryAt && now < retryAt)
        {
            return null;
        }

        if (state.StoppedSince is { } stoppedSince && now - stoppedSince >= EndAfterStopped)
        {
            return new LiveActivityPushPlan(LiveActivityPushKind.End, LiveActivityPushPlan.HighPriority);
        }

        var change = PlayerStatusChangeDetector.Compare(state.LastSent, current);
        if (!change.IsMaterial)
        {
            return null;
        }

        if (change.IsHighPriority)
        {
            return new LiveActivityPushPlan(LiveActivityPushKind.Update, LiveActivityPushPlan.HighPriority);
        }

        if (state.LastLowPrioritySentAt is { } lastSent && now - lastSent < LowPriorityInterval)
        {
            return null;
        }

        return new LiveActivityPushPlan(LiveActivityPushKind.Update, LiveActivityPushPlan.LowPriority);
    }
}
