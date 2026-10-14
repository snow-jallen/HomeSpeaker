using System.Collections.Concurrent;

namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// In-memory set of Live Activities to keep updated. Nothing is persisted: a Live Activity can't
/// outlive <see cref="RegistrationLifetime"/>, and after a restart the app re-registers.
/// </summary>
public sealed class LiveActivityRegistry
{
    /// <summary>iOS ends a Live Activity after 8 hours and removes it from the lock screen 4 hours later.</summary>
    public static readonly TimeSpan RegistrationLifetime = TimeSpan.FromHours(12);

    private const int MaxActivityIdLength = 200;

    private readonly ConcurrentDictionary<string, LiveActivityRegistration> registrations = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;

    public LiveActivityRegistry(TimeProvider timeProvider) => this.timeProvider = timeProvider;

    /// <summary>The number of unexpired registrations.</summary>
    public int Count => GetActive().Count;

    /// <summary>
    /// Registers the push token for an activity, replacing any earlier token for it (iOS rotates
    /// them). A new token keeps the original expiry, since it is still the same activity.
    /// </summary>
    /// <exception cref="ArgumentException">The activity id or push token is invalid.</exception>
    public LiveActivityRegistration Register(string activityId, string? pushToken)
    {
        var normalizedId = normalizeActivityId(activityId);
        var normalizedToken = ApnsToken.Normalize(pushToken ?? string.Empty);
        if (!ApnsToken.IsValid(normalizedToken))
        {
            throw new ArgumentException("PushToken must be a hex-encoded APNs push token.", nameof(pushToken));
        }

        var now = timeProvider.GetUtcNow();
        return registrations.AddOrUpdate(
            normalizedId,
            id => new LiveActivityRegistration(id, normalizedToken, now, now + RegistrationLifetime),
            (id, existing) =>
            {
                if (existing.ExpiresAt <= now)
                {
                    return new LiveActivityRegistration(id, normalizedToken, now, now + RegistrationLifetime);
                }

                // Re-registering the same token (e.g. the app re-sending it on launch) is a no-op,
                // so it doesn't reset the push state and trigger a needless push.
                return string.Equals(existing.PushToken, normalizedToken, StringComparison.OrdinalIgnoreCase)
                    ? existing
                    : new LiveActivityRegistration(id, normalizedToken, existing.RegisteredAt, existing.ExpiresAt);
            });
    }

    /// <summary>Removes an activity. Returns false when it wasn't registered.</summary>
    public bool Remove(string activityId)
        => !string.IsNullOrWhiteSpace(activityId) && registrations.TryRemove(activityId.Trim(), out _);

    /// <summary>
    /// Removes this exact registration, leaving alone a newer one that replaced it meanwhile.
    /// </summary>
    public bool Remove(LiveActivityRegistration registration)
        => registrations.TryRemove(new KeyValuePair<string, LiveActivityRegistration>(registration.ActivityId, registration));

    /// <summary>The unexpired registrations; expired ones are dropped as a side effect.</summary>
    public IReadOnlyList<LiveActivityRegistration> GetActive()
    {
        var now = timeProvider.GetUtcNow();
        var active = new List<LiveActivityRegistration>(registrations.Count);
        foreach (var registration in registrations.Values)
        {
            if (registration.ExpiresAt <= now)
            {
                Remove(registration);
            }
            else
            {
                active.Add(registration);
            }
        }

        return active;
    }

    private static string normalizeActivityId(string activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId) || activityId.Trim().Length > MaxActivityIdLength)
        {
            throw new ArgumentException($"ActivityId is required and must be {MaxActivityIdLength} characters or fewer.", nameof(activityId));
        }

        return activityId.Trim();
    }
}
