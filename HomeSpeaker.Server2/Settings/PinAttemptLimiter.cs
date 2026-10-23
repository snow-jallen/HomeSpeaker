using System.Collections.Concurrent;

namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// Throttles PIN guesses per client: 5 failures within 60 s lock that client out for 60 s from the
/// 5th failure. Kept in memory, so a restart clears it.
/// </summary>
public sealed class PinAttemptLimiter(TimeProvider timeProvider)
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan window = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, State> clients = new(StringComparer.Ordinal);

    public bool IsLocked(string clientKey)
    {
        if (!clients.TryGetValue(clientKey, out var state))
        {
            return false;
        }

        lock (state)
        {
            return state.LockedUntil > timeProvider.GetUtcNow();
        }
    }

    public void RecordFailure(string clientKey)
    {
        var now = timeProvider.GetUtcNow();
        var state = clients.GetOrAdd(clientKey, _ => new State());
        lock (state)
        {
            state.Failures.RemoveAll(t => now - t >= window);
            state.Failures.Add(now);
            if (state.Failures.Count >= MaxFailures)
            {
                state.LockedUntil = now + window;
                state.Failures.Clear();
            }
        }
    }

    public void Reset(string clientKey) => clients.TryRemove(clientKey, out _);

    private sealed class State
    {
        public List<DateTimeOffset> Failures { get; } = [];

        public DateTimeOffset LockedUntil { get; set; }
    }
}
