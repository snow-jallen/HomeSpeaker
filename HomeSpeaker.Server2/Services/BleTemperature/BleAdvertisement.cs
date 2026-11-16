namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>One Bluetooth LE advertisement as it arrived: who sent it, how strong, and its manufacturer data by company id.</summary>
public sealed record BleAdvertisement(string Mac, string? Name, int Rssi, IReadOnlyDictionary<ushort, byte[]> ManufacturerData);
