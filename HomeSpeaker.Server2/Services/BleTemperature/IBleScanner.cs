namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>Listens for Bluetooth LE advertisements for a while.</summary>
public interface IBleScanner
{
    /// <summary>
    /// Scans for <paramref name="duration"/>, calling <paramref name="onAdvertisement"/> (possibly from another thread)
    /// for each advertisement received during the scan, never for data cached from before it.
    /// </summary>
    Task ScanAsync(TimeSpan duration, Action<BleAdvertisement> onAdvertisement, CancellationToken cancellationToken);
}
