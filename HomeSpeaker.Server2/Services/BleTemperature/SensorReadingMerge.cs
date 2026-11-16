using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Which of two readings of the same sensor (possibly heard by different speakers) the hub keeps: a reading more than
/// a minute newer always wins; closer than that, the stronger signal wins, then the newer one. A speaker's own newer
/// reading always replaces its older one.
/// </summary>
public static class SensorReadingMerge
{
    private static readonly TimeSpan sameMoment = TimeSpan.FromMinutes(1);

    /// <summary>True when <paramref name="incoming"/> should replace <paramref name="current"/>.</summary>
    public static bool Prefers(SensorReading? current, SensorReading incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (current is null)
        {
            return true;
        }

        var newerBy = incoming.ObservedAt - current.ObservedAt;
        if (string.Equals(current.Reporter, incoming.Reporter, StringComparison.OrdinalIgnoreCase))
        {
            return newerBy > TimeSpan.Zero;
        }

        if (newerBy > sameMoment)
        {
            return true;
        }

        if (newerBy < -sameMoment)
        {
            return false;
        }

        return incoming.Rssi != current.Rssi ? incoming.Rssi > current.Rssi : newerBy > TimeSpan.Zero;
    }
}
