namespace HomeSpeaker.Shared.WindowMonitoring;

public sealed class PushRegistrationStatusDto
{
    public string InstallationId { get; set; } = string.Empty;

    public string? Platform { get; set; }

    public bool IsRegistered { get; set; }

    public string? BundleId { get; set; }

    public DateTime? DeviceTokenUpdatedUtc { get; set; }

    public bool TemperatureAlertsEnabled { get; set; }

    public bool BloodSugarAlertsEnabled { get; set; }
}
