using HomeSpeaker.Server2.Data;
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}