using System.Threading.Channels;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// One consumer's feed of material player-status changes from <see cref="PlayerStatusBroadcaster"/>.
/// Holds only the newest undelivered snapshot, so a slow consumer never makes it buffer.
/// Dispose to unsubscribe.
/// </summary>
public sealed class PlayerStatusSubscription : IDisposable
{
    private readonly Channel<PlayerStatusSnapshot> channel = Channel.CreateBounded<PlayerStatusSnapshot>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Action<PlayerStatusSubscription> onDispose;
    private readonly TimeProvider timeProvider;
    private Task<PlayerStatusSnapshot>? pendingRead;
    private int disposed;

    public PlayerStatusSubscription(Action<PlayerStatusSubscription> onDispose, TimeProvider timeProvider)
    {
        this.onDispose = onDispose;
        this.timeProvider = timeProvider;
    }

    /// <summary>Queues a snapshot for the consumer, replacing any it hasn't read yet.</summary>
    public void Publish(PlayerStatusSnapshot snapshot) => channel.Writer.TryWrite(snapshot);

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for the next published snapshot; returns null on
    /// timeout. A read that times out stays pending for the next call (so repeated timeouts don't
    /// pile up channel waiters), which means callers must pass the same token on every call.
    /// </summary>
    public async Task<PlayerStatusSnapshot?> WaitForUpdateAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        pendingRead ??= channel.Reader.ReadAsync(cancellationToken).AsTask();

        if (!pendingRead.IsCompleted)
        {
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(timeout, timeProvider, delayCancellation.Token);
            var completed = await Task.WhenAny(pendingRead, delay);
            await delayCancellation.CancelAsync();

            if (completed != pendingRead)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return null;
            }
        }

        var read = pendingRead;
        pendingRead = null;
        return await read;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        channel.Writer.TryComplete();
        onDispose(this);
    }
}
