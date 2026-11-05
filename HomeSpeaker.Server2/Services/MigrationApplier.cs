using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services.Anchors;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class MigrationApplier : IHostedService
{
    private readonly IServiceProvider service;
    private readonly ILogger<MigrationApplier> logger;

    public MigrationApplier(IServiceProvider service, ILogger<MigrationApplier> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = service.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<MusicContext>();
            logger.LogInformation("Applying migrations...");
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied successfully!");

            await importLegacyAnchorsAsync(scope.ServiceProvider, context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "***  Trouble applying migrations!");

            if (System.Diagnostics.Debugger.IsAttached)
            {
                logger.LogWarning("Maybe it's a connection string issue, or the database is not up?\n");
            }

            throw;
        }
    }

    /// <summary>
    /// Moves the old anchor tables into the new ones. A failure here must not keep the speaker from starting: the old
    /// tables stay put and the next start tries again.
    /// </summary>
    private async Task importLegacyAnchorsAsync(IServiceProvider services, MusicContext context, CancellationToken cancellationToken)
    {
        try
        {
            var today = AnchorClock.Today(services.GetRequiredService<TimeProvider>());
            var imported = await LegacyAnchorImporter.ImportAsync(context, today, cancellationToken);
            if (imported > 0)
            {
                logger.LogInformation("Moved {Count} people's anchors from the old anchor tables", imported);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Couldn't move the old anchor tables; will try again at the next start");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
