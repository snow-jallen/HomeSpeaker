using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class MigrationApplier : IHostedService
{
    private readonly IServiceProvider _service;
    private readonly ILogger<MigrationApplier> _logger;

    public MigrationApplier(IServiceProvider service, ILogger<MigrationApplier> logger)
    {
        _service = service;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using (var scope = _service.CreateScope())
        {
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<MusicContext>();
                _logger.LogInformation("Applying migrations...");
                context.Database.Migrate();
                _logger.LogInformation("Migrations applied successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "***  Trouble applying migrations!");

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    _logger.LogWarning("Maybe it's a connection string issue, or the database is not up?\n");
                }
                throw;
            }
            return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}