namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>Lets the /api/photos/sync endpoint wake the worker early.</summary>
public sealed class PhotoSyncTrigger : IDisposable
{
    private readonly SemaphoreSlim signal = new(0, 1);

    public void Request()
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A sync is already requested.
        }
    }

    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => signal.WaitAsync(timeout, cancellationToken);

    public void Dispose() => signal.Dispose();
}
