namespace HomeSpeaker.Shared;

public record PushDeviceRegistrationDto
{
    public string InstallationId { get; init; } = string.Empty;
    public PushNotificationPlatform Platform { get; init; }
    public string? DeviceName { get; init; }
    public bool TemperatureAlertsEnabled { get; init; }
    public bool BloodSugarAlertsEnabled { get; init; }
    public bool IsActive { get; init; }
    public DateTime RegisteredUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}
