namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>What one Govee thermo-hygrometer advertisement says.</summary>
public sealed record GoveeFrame(double TemperatureC, double Humidity, int BatteryPercent);
