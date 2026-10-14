namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// A Live Activity the server keeps updated. <see cref="Push"/> is mutated only by the push
/// dispatcher; re-registering an activity replaces the whole registration (and its push state).
/// </summary>
public sealed class LiveActivityRegistration
{
    public LiveActivityRegistration(string activityId, string pushToken, DateTimeOffset registeredAt, DateTimeOffset expiresAt)
    {
        ActivityId = activityId;
        PushToken = pushToken;
        RegisteredAt = registeredAt;
        ExpiresAt = expiresAt;
    }

    public string ActivityId { get; }

    public string PushToken { get; }

    public DateTimeOffset RegisteredAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public LiveActivityPushState Push { get; } = new();
}
