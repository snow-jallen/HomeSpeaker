using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared.Temperature;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// The best current reading of every Bluetooth sensor this speaker knows about: its own scans and, on the hub, what
/// the other speakers send. <see cref="SensorReadingMerge"/> decides between two readings of one sensor. The scan
/// worker saves changed readings to the database each cycle and loads them at startup.
/// </summary>
public sealed class SensorReadingStore
{
    /// <summary>More sensors than any household has; the readings endpoint is open, so the store is bounded.</summary>
    public const int MaxSensors = 64;

    /// <summary>A sensor not heard for this long is forgotten.</summary>
    public static readonly TimeSpan ForgetAfter = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();
    private readonly Dictionary<string, SensorReading> readings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> changed = new(StringComparer.OrdinalIgnoreCase);

    public SensorReadingStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
    }

    private DateTime ForgetBefore => timeProvider.GetUtcNow().UtcDateTime - ForgetAfter;

    public IReadOnlyCollection<SensorReading> All
    {
        get
        {
            lock (gate)
            {
                var cutoff = ForgetBefore;
                return [.. readings.Values.Where(r => r.ObservedAt >= cutoff).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)];
            }
        }
    }

    /// <summary>
    /// Keeps <paramref name="reading"/> if it beats the current one for its sensor; true when it did. A week-old reading,
    /// or a new sensor once <see cref="MaxSensors"/> are known, is refused.
    /// </summary>
    public bool Record(SensorReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        reading = reading with { Mac = reading.Mac.ToUpperInvariant() };
        var cutoff = ForgetBefore;
        if (reading.ObservedAt < cutoff)
        {
            return false;
        }

        lock (gate)
        {
            readings.TryGetValue(reading.Mac, out var current);
            if (current is null && readings.Count >= MaxSensors)
            {
                foreach (var old in readings.Values.Where(r => r.ObservedAt < cutoff).Select(r => r.Mac).ToList())
                {
                    readings.Remove(old);
                }

                if (readings.Count >= MaxSensors)
                {
                    return false;
                }
            }

            if (!SensorReadingMerge.Prefers(current, reading))
            {
                return false;
            }

            readings[reading.Mac] = reading;
            changed.Add(reading.Mac);
            return true;
        }
    }

    /// <summary>Adds the saved readings, keeping any newer one already heard.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MusicContext>();
        var saved = await context.BluetoothSensorReadings.AsNoTracking().ToListAsync(cancellationToken);
        var cutoff = ForgetBefore;
        lock (gate)
        {
            foreach (var entity in saved.Where(e => DateTime.SpecifyKind(e.ObservedAt, DateTimeKind.Utc) >= cutoff).Take(MaxSensors))
            {
                var reading = new SensorReading
                {
                    Mac = entity.Mac,
                    Name = entity.Name,
                    TemperatureC = entity.TemperatureC,
                    Humidity = entity.Humidity,
                    BatteryPercent = entity.BatteryPercent,
                    Rssi = entity.Rssi,
                    ObservedAt = DateTime.SpecifyKind(entity.ObservedAt, DateTimeKind.Utc),
                    Reporter = entity.Reporter,
                };
                if (!readings.TryGetValue(reading.Mac, out var current) || current.ObservedAt < reading.ObservedAt)
                {
                    readings[reading.Mac] = reading;
                }
            }
        }
    }

    /// <summary>Writes the readings that changed since the last save.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        List<SensorReading> toSave;
        lock (gate)
        {
            toSave = [.. changed.Select(mac => readings[mac])];
            changed.Clear();
        }

        if (toSave.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<MusicContext>();
            var macs = toSave.Select(r => r.Mac).ToList();
            var existing = await context.BluetoothSensorReadings.Where(e => macs.Contains(e.Mac)).ToDictionaryAsync(e => e.Mac, cancellationToken);
            foreach (var reading in toSave)
            {
                if (!existing.TryGetValue(reading.Mac, out var entity))
                {
                    entity = new BluetoothSensorReadingEntity { Mac = reading.Mac };
                    context.BluetoothSensorReadings.Add(entity);
                }

                entity.Name = reading.Name;
                entity.TemperatureC = reading.TemperatureC;
                entity.Humidity = reading.Humidity;
                entity.BatteryPercent = reading.BatteryPercent;
                entity.Rssi = reading.Rssi;
                entity.ObservedAt = reading.ObservedAt;
                entity.Reporter = reading.Reporter;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Try again next time.
            lock (gate)
            {
                foreach (var reading in toSave)
                {
                    changed.Add(reading.Mac);
                }
            }

            throw;
        }
    }
}
