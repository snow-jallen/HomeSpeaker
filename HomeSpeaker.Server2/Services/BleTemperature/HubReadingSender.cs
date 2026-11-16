using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Sends this speaker's readings to the hub. A reading the hub didn't take stays queued (only the newest per sensor)
/// and goes with the next send, so a short hub outage loses nothing that still matters.
/// </summary>
public sealed class HubReadingSender
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<HubReadingSender> logger;
    private readonly Dictionary<string, SensorReading> pending = new(StringComparer.OrdinalIgnoreCase);
    private bool failing;

    public HubReadingSender(IHttpClientFactory httpClientFactory, ILogger<HubReadingSender> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>Queues <paramref name="readings"/> and posts everything queued. Called by one worker at a time.</summary>
    public async Task SendAsync(Uri hub, IEnumerable<SensorReading> readings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(readings);

        // The newest heard wins even if its clock reads earlier (a clock NTP just corrected).
        foreach (var reading in readings)
        {
            pending[reading.Mac] = reading;
        }

        if (pending.Count == 0)
        {
            return;
        }

        var batch = pending.Values.ToList();
        try
        {
            var client = httpClientFactory.CreateClient(BluetoothTemperatureService.HubClient);
            using var response = await client.PostAsJsonAsync(new Uri(hub, "api/temperature/readings"), batch, cancellationToken);
            if ((int)response.StatusCode is >= 400 and < 500)
            {
                // The hub refused these (bad data, or it isn't the hub): resending them unchanged would block every
                // later reading, so drop them and let the next scan send fresh ones.
                logger.LogWarning(
                    "Temperature hub {Hub} refused {Count} readings ({Status}: {Reason}); dropping them.",
                    hub,
                    batch.Count,
                    (int)response.StatusCode,
                    await response.Content.ReadAsStringAsync(cancellationToken));
                removeSent(batch);
                return;
            }

            response.EnsureSuccessStatusCode();
            removeSent(batch);

            if (failing)
            {
                logger.LogInformation("Temperature hub {Hub} is taking readings again.", hub);
                failing = false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (!failing)
            {
                logger.LogWarning("Couldn't send {Count} temperature readings to the hub {Hub} ({Error}); will retry.", batch.Count, hub, ex.Message);
                failing = true;
            }
        }
    }

    private void removeSent(List<SensorReading> batch)
    {
        foreach (var sent in batch)
        {
            if (pending.TryGetValue(sent.Mac, out var queued) && queued == sent)
            {
                pending.Remove(sent.Mac);
            }
        }
    }
}
