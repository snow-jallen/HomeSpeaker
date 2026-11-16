using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// One scan burst: listen, decode the Govee frames heard, keep them in this speaker's <see cref="SensorReadingStore"/>,
/// send them to the hub when this speaker isn't it, and save what changed.
/// </summary>
public sealed class BleScanCycle
{
    private readonly IBleScanner scanner;
    private readonly SensorReadingStore store;
    private readonly HubReadingSender sender;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BleScanCycle> logger;
    private readonly string reporter;

    public BleScanCycle(
        IBleScanner scanner,
        SensorReadingStore store,
        HubReadingSender sender,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<BleScanCycle> logger,
        string reporter)
    {
        this.scanner = scanner;
        this.store = store;
        this.sender = sender;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.reporter = reporter;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var heard = new Dictionary<string, SensorReading>(StringComparer.OrdinalIgnoreCase);
        var gate = new Lock();
        await scanner.ScanAsync(TemperatureSource.ScanTiming(configuration).Burst, advertisement =>
        {
            // 0x0001 is Nokia's company id, not Govee's: like govee-ble, trust the frame only from a Govee name.
            if (!GoveeAdvertisement.IsGoveeName(advertisement.Name) ||
                !advertisement.ManufacturerData.TryGetValue(GoveeAdvertisement.CompanyId, out var data) ||
                GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, data) is not { } frame)
            {
                return;
            }

            var reading = new SensorReading
            {
                Mac = advertisement.Mac.ToUpperInvariant(),
                Name = advertisement.Name!,
                TemperatureC = frame.TemperatureC,
                Humidity = frame.Humidity,
                BatteryPercent = frame.BatteryPercent,
                Rssi = advertisement.Rssi,
                ObservedAt = timeProvider.GetUtcNow().UtcDateTime,
                Reporter = reporter,
            };
            lock (gate)
            {
                heard[reading.Mac] = reading;
            }
        }, cancellationToken);

        foreach (var reading in heard.Values)
        {
            store.Record(reading);
        }

        logger.LogDebug("Bluetooth scan heard {Count} temperature sensors.", heard.Count);
        if (TemperatureSource.HubUrl(configuration) is { } hub)
        {
            await sender.SendAsync(hub, heard.Values, cancellationToken);
        }

        try
        {
            await store.SaveAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Couldn't save Bluetooth temperature readings.");
        }
    }
}
