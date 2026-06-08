using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Periodically polls the temperature and blood-sugar monitors and sends APNs alerts when
/// their state warrants it. This is the single source of proactive push notifications — the
/// Blazor components and REST endpoints only read status now, they no longer trigger sends.
/// </summary>
public sealed class HealthAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IConfiguration configuration;
    private readonly PushNotificationOptions options;
    private readonly ILogger<HealthAlertWorker> logger;

    public HealthAlertWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<PushNotificationOptions> options,
        ILogger<HealthAlertWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.configuration = configuration;
        this.options = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CanSend)
        {
            logger.LogInformation("HealthAlertWorker is idle: push notifications are not configured.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(30, options.PollIntervalSeconds));
        logger.LogInformation("HealthAlertWorker started; evaluating health alerts every {Seconds}s.", interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await pollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "HealthAlertWorker poll failed.");
            }
        }
        while (await waitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> waitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task pollOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

        if (temperatureConfigured())
        {
            try
            {
                var temperatureService = scope.ServiceProvider.GetRequiredService<ITemperatureService>();
                var status = await temperatureService.GetTemperatureStatusAsync(cancellationToken);
                await pushNotificationService.NotifyTemperatureStatusAsync(status, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate temperature alerts.");
            }
        }

        if (bloodSugarConfigured())
        {
            try
            {
                var bloodSugarService = scope.ServiceProvider.GetRequiredService<IBloodSugarService>();
                var status = await bloodSugarService.GetBloodSugarStatusAsync(cancellationToken);
                await pushNotificationService.NotifyBloodSugarStatusAsync(status, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate blood sugar alerts.");
            }
        }
    }

    private bool temperatureConfigured() =>
        !string.IsNullOrWhiteSpace(configuration["Temperature:ApiKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Temperature:ApiBaseUrl"]);

    private bool bloodSugarConfigured() =>
        !string.IsNullOrWhiteSpace(configuration["NIGHTSCOUT_URL"]);
}
