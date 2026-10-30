namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Spaces calls at least <c>interval</c> apart, across all callers (MusicBrainz allows one request per second).</summary>
public sealed class RequestThrottle(TimeSpan interval, TimeProvider timeProvider) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextAllowed = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wait = nextAllowed - timeProvider.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            nextAllowed = timeProvider.GetUtcNow() + interval;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
