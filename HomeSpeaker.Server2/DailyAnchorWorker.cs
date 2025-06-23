using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2;

public class DailyAnchorWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<DailyAnchorWorker> logger;

    public DailyAnchorWorker(IServiceProvider serviceProvider, ILogger<DailyAnchorWorker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Daily Anchor Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var anchorService = scope.ServiceProvider.GetRequiredService<AnchorService>();
                    await anchorService.EnsureTodayAnchorsForAllUsersAsync();
                    logger.LogInformation("Daily anchors ensured for all users");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ensuring daily anchors");
            }

            // Wait until the next day at midnight
            var now = DateTime.Now;
            var tomorrow = now.Date.AddDays(1);
            var delay = tomorrow - now;
            
            // If delay is less than 1 minute, add a day (to handle edge cases)
            if (delay.TotalMinutes < 1)
            {
                delay = delay.Add(TimeSpan.FromDays(1));
            }

            logger.LogInformation("Next daily anchor creation scheduled for {time} (in {hours} hours)", 
                now.Add(delay), delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped
                break;
            }
        }

        logger.LogInformation("Daily Anchor Worker stopped");
    }
}
