namespace HomeSpeaker.Shared;

public record PushDeviceRegistrationRequest
{
    public string InstallationId { get; init; } = string.Empty;
    public PushNotificationPlatform Platform { get; init; } = PushNotificationPlatform.Apns;
    public string DeviceToken { get; init; } = string.Empty;
    public string? DeviceName { get; init; }
    public bool TemperatureAlertsEnabled { get; init; } = true;
    public bool BloodSugarAlertsEnabled { get; init; } = true;
}
