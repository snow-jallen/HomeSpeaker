using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// Pushes the player status to every registered Live Activity that needs it, applying the
/// throttling and end-of-life rules in <see cref="LiveActivityPushPlanner"/>.
/// </summary>
public sealed class LiveActivityPushDispatcher
{
    /// <summary>How long to wait before retrying an activity after a transient APNs failure.</summary>
    public static readonly TimeSpan TransientRetryDelay = TimeSpan.FromSeconds(30);

    private readonly LiveActivityRegistry registry;
    private readonly ILiveActivityPushSender sender;
    private readonly PushNotificationOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<LiveActivityPushDispatcher> logger;
    private int loggedNotConfigured;

    public LiveActivityPushDispatcher(
        LiveActivityRegistry registry,
        ILiveActivityPushSender sender,
        IOptions<PushNotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<LiveActivityPushDispatcher> logger)
    {
        this.registry = registry;
        this.sender = sender;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>
    /// True when there are activities to update and APNs is configured to update them. When
    /// activities are registered but APNs isn't configured, says so in the log (once).
    /// </summary>
    public bool HasWork()
    {
        if (registry.Count == 0)
        {
            return false;
        }

        if (!options.CanSend)
        {
            if (Interlocked.Exchange(ref loggedNotConfigured, 1) == 0)
            {
                logger.LogInformation("Live Activities are registered but APNs is not configured; they won't be updated.");
            }

            return false;
        }

        return true;
    }

    /// <summary>Sends whatever pushes <paramref name="current"/> calls for, in parallel.</summary>
    public async Task ProcessAsync(PlayerStatusSnapshot current, CancellationToken cancellationToken)
    {
        if (!HasWork())
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var pushes = new List<Task>();
        foreach (var registration in registry.GetActive())
        {
            var plan = LiveActivityPushPlanner.Plan(registration.Push, current, now);
            if (plan is not null)
            {
                pushes.Add(sendAsync(registration, plan, current, now, cancellationToken));
            }
        }

        await Task.WhenAll(pushes);
    }

    private async Task sendAsync(
        LiveActivityRegistration registration,
        LiveActivityPushPlan plan,
        PlayerStatusSnapshot current,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var contentState = LiveActivityPayload.CreateContentState(current);
        var payload = plan.Kind == LiveActivityPushKind.End
            ? LiveActivityPayload.BuildEnd(contentState, now)
            : LiveActivityPayload.BuildUpdate(contentState, now);

        PushNotificationSendResult result;
        try
        {
            result = await sender.SendLiveActivityAsync(registration.PushToken, payload, plan.Priority, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            result = new PushNotificationSendResult(false, ErrorCode: "live-activity-push-failed", ErrorMessage: ex.Message);
        }

        if (plan.Kind == LiveActivityPushKind.End)
        {
            registry.Remove(registration);
            logger.LogInformation("Ended Live Activity {ActivityId} after playback stayed stopped", registration.ActivityId);
            return;
        }

        if (result.ShouldDeactivateDevice)
        {
            registry.Remove(registration);
            logger.LogInformation("Dropped Live Activity {ActivityId}: APNs rejected its token ({Reason})", registration.ActivityId, result.ErrorCode);
            return;
        }

        var state = registration.Push;
        if (result.IsSuccess)
        {
            state.LastSent = current;
            state.RetryNotBefore = null;
            if (plan.Priority == LiveActivityPushPlan.LowPriority)
            {
                state.LastLowPrioritySentAt = now;
            }

            return;
        }

        state.RetryNotBefore = now + TransientRetryDelay;
        logger.LogWarning(
            "Live Activity push to {ActivityId} failed ({Reason}); retrying in {Delay}",
            registration.ActivityId,
            result.ErrorCode,
            TransientRetryDelay);
    }
}
