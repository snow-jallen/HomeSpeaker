namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Runs a <see cref="BleScanCycle"/> every <c>Temperature:Bluetooth:ScanEverySeconds</c> while Bluetooth is the
/// temperature source. Registered only when it was the source at startup (changing the source needs a restart).
/// Without a reachable BlueZ (no adapter, no D-Bus mount) it logs and keeps retrying slowly; the hub still serves
/// readings other speakers send.
/// </summary>
public sealed class BleTemperatureWorker : BackgroundService
{
    private static readonly TimeSpan retryAfterFailure = TimeSpan.FromMinutes(5);

    private readonly BleScanCycle cycle;
    private readonly SensorReadingStore store;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BleTemperatureWorker> logger;

    public BleTemperatureWorker(BleScanCycle cycle, SensorReadingStore store, IConfiguration configuration, TimeProvider timeProvider, ILogger<BleTemperatureWorker> logger)
    {
        this.cycle = cycle;
        this.store = store;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await store.LoadAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Couldn't load saved Bluetooth temperature readings.");
        }

        var failing = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = TemperatureSource.ScanTiming(configuration);
            var pause = wait.Every - wait.Burst;
            if (TemperatureSource.IsBluetooth(configuration))
            {
                try
                {
                    await cycle.RunOnceAsync(stoppingToken);
                    if (failing)
                    {
                        logger.LogInformation("Bluetooth temperature scanning works again.");
                        failing = false;
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    if (!failing)
                    {
                        logger.LogWarning(ex, "Bluetooth temperature scan failed (is /run/dbus mounted and is there an adapter?); retrying every {Minutes} min.", retryAfterFailure.TotalMinutes);
                        failing = true;
                    }

                    pause = retryAfterFailure;
                }
            }

            try
            {
                await Task.Delay(pause, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
