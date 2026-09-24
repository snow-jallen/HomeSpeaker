namespace HomeSpeaker.Shared.WindowMonitoring;

public sealed class UpsertPushInstallationRequest
{
    public string InstallationId { get; set; } = string.Empty;

    public string Platform { get; set; } = "ios";

    public string DeviceToken { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public string? BundleId { get; set; }

    public bool TemperatureAlertsEnabled { get; set; }

    public bool BloodSugarAlertsEnabled { get; set; }
}
