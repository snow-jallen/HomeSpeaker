using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2;

public class DailyAnchorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyAnchorWorker> _logger;

    public DailyAnchorWorker(IServiceProvider serviceProvider, ILogger<DailyAnchorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Daily Anchor Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var anchorService = scope.ServiceProvider.GetRequiredService<AnchorService>();
                    await anchorService.EnsureTodayAnchorsForAllUsersAsync();
                    _logger.LogInformation("Daily anchors ensured for all users");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring daily anchors");
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

            _logger.LogInformation("Next daily anchor creation scheduled for {time} (in {hours} hours)", 
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

        _logger.LogInformation("Daily Anchor Worker stopped");
    }
}
