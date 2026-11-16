namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Decodes the manufacturer data Govee H5100-family sensors broadcast (the encoding Home Assistant's govee-ble uses
/// for H5100/H5101/H5102/H5104): six bytes under company id 0x0001, where bytes 2-4 are one big-endian number
/// <c>n</c> (top bit = below zero), temperature = floor(n / 1000) / 10 °C, humidity = (n mod 1000) / 10 %, and byte 5
/// is the battery percent.
/// </summary>
public static class GoveeAdvertisement
{
    public const ushort CompanyId = 0x0001;

    private const int FrameLength = 6;
    private const int NegativeBit = 0x800000;
    private const double MinCelsius = -50;
    private const double MaxCelsius = 100;

    /// <summary>True for the names H5100-family sensors advertise (GVH5100_1776, GVH5104_...).</summary>
    public static bool IsGoveeName(string? name) =>
        name is { Length: <= 32 } && name.StartsWith("GVH5", StringComparison.OrdinalIgnoreCase);

    /// <summary>The reading, or null when the data isn't a plausible frame in this format.</summary>
    public static GoveeFrame? Decode(ushort companyId, ReadOnlySpan<byte> data)
    {
        if (companyId != CompanyId || data.Length != FrameLength)
        {
            return null;
        }

        var packed = (data[2] << 16) | (data[3] << 8) | data[4];
        var negative = (packed & NegativeBit) != 0;
        packed &= ~NegativeBit;

        var celsius = packed / 1000 / 10.0;
        if (negative)
        {
            celsius = -celsius;
        }

        var humidity = packed % 1000 / 10.0;
        int battery = data[5];
        if (battery > 100 || celsius is < MinCelsius or > MaxCelsius)
        {
            return null;
        }

        return new GoveeFrame(celsius, humidity, battery);
    }
}
