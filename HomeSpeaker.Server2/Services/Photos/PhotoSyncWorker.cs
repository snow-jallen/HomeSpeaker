using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

public sealed class PhotoSyncWorker(
    IServiceScopeFactory scopeFactory,
    PhotoSyncTrigger trigger,
    IOptions<ImmichOptions> options,
    ILogger<PhotoSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var immich = options.Value;
        if (!immich.IsConfigured)
        {
            logger.LogInformation("Immich is not configured; the photo slideshow is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, immich.SyncIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PhotoSyncer>().SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Photo sync failed unexpectedly");
            }

            try
            {
                await trigger.WaitAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
