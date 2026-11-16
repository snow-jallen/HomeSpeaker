using HomeSpeaker.Server2.Services.BleTemperature;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class GoveeAdvertisementTests
{
    private static byte[] hex(string text) => Convert.FromHexString(text.Replace(" ", "", StringComparison.Ordinal));

    // Real frames captured from the household's H5100 sensors; the expected values follow the govee-ble encoding.
    [Theory]
    [InlineData("01 01 03 1e da 64", 20.4, 50.6, 100)]
    [InlineData("01 01 03 22 c3 64", 20.5, 50.7, 100)]
    [InlineData("01 01 03 26 ac 64", 20.6, 50.8, 100)]
    [InlineData("01 01 03 41 ff 64", 21.3, 50.3, 100)]
    [InlineData("01 01 02 50 35 60", 15.1, 60.5, 96)]
    [InlineData("01 01 03 0f 3c 15", 20.0, 50.8, 21)]
    [InlineData("01 01 03 13 28 0a", 20.1, 51.2, 10)]
    [InlineData("01 01 03 03 9e 64", 19.7, 53.4, 100)]
    [InlineData("01 01 03 0b 5a 64", 19.9, 51.4, 100)]
    [InlineData("01 01 00 ca 33 62", 5.1, 76.3, 98)]
    [InlineData("01 01 02 e0 9b 64", 18.8, 57.1, 100)]
    public void Decode_RealFrames(string frame, double celsius, double humidity, int battery)
    {
        var reading = GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, hex(frame));

        Assert.NotNull(reading);
        Assert.Equal(celsius, reading.TemperatureC, 3);
        Assert.Equal(humidity, reading.Humidity, 3);
        Assert.Equal(battery, reading.BatteryPercent);
    }

    [Fact]
    public void Decode_SignBitMeansBelowFreezing()
    {
        var reading = GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, hex("01 01 80 c3 50 64"));

        Assert.NotNull(reading);
        Assert.Equal(-5.0, reading.TemperatureC, 3);
        Assert.Equal(0.0, reading.Humidity, 3);
    }

    [Fact]
    public void Decode_OtherCompanyId_IsNull() =>
        Assert.Null(GoveeAdvertisement.Decode(0x004c, hex("01 01 03 1e da 64")));

    [Fact]
    public void Decode_GatewayFrame_IsNull() =>
        Assert.Null(GoveeAdvertisement.Decode(0x8801, hex("01 01 03 1e da 64")));

    [Theory]
    [InlineData("01 01 03 1e da")]
    [InlineData("01 01 03 1e da 64 00")]
    [InlineData("")]
    public void Decode_WrongLength_IsNull(string frame) =>
        Assert.Null(GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, hex(frame)));

    [Fact]
    public void Decode_BatteryOver100_IsNull() =>
        Assert.Null(GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, hex("01 01 03 1e da 65")));

    [Fact]
    public void Decode_ImplausibleTemperature_IsNull() =>
        // 0x7fffff: 838.8 °C.
        Assert.Null(GoveeAdvertisement.Decode(GoveeAdvertisement.CompanyId, hex("01 01 7f ff ff 64")));
}
