using HomeSpeaker.Server2.Services;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Temperature;

public sealed class TemperatureDisplayTests
{
    [Theory]
    [InlineData(212, 100)]
    [InlineData(32, 0)]
    public void Convert_ToCelsius(double fahrenheit, double expected) =>
        Assert.Equal(expected, TemperatureDisplay.Convert(fahrenheit, TemperatureUnit.Celsius), 6);

    [Fact]
    public void Convert_Fahrenheit_IsIdentity() => Assert.Equal(72.5, TemperatureDisplay.Convert(72.5, TemperatureUnit.Fahrenheit));

    [Fact]
    public void ConvertDelta_ToCelsius_HasNoOffset() => Assert.Equal(5, TemperatureDisplay.ConvertDelta(9, TemperatureUnit.Celsius), 6);

    [Fact]
    public void ToFahrenheit_FromCelsius() => Assert.Equal(50, TemperatureDisplay.ToFahrenheit(10, TemperatureUnit.Celsius), 6);

    [Theory]
    [InlineData(72.5, "73°")]
    [InlineData(72.4, "72°")]
    [InlineData(-0.5, "-1°")]
    public void Format_Fahrenheit_RoundsAwayFromZero(double value, string expected) =>
        Assert.Equal(expected, TemperatureDisplay.Format(value, TemperatureUnit.Fahrenheit));

    [Fact]
    public void Format_Celsius_ConvertsThenRounds() => Assert.Equal("22°", TemperatureDisplay.Format(72, TemperatureUnit.Celsius));

    [Fact]
    public void Format_Null_IsDash() => Assert.Equal("–", TemperatureDisplay.Format(null, TemperatureUnit.Fahrenheit));

    [Theory]
    [InlineData("c", TemperatureUnit.Celsius)]
    [InlineData("C", TemperatureUnit.Celsius)]
    [InlineData("Celsius", TemperatureUnit.Celsius)]
    [InlineData("celsius", TemperatureUnit.Celsius)]
    [InlineData("F", TemperatureUnit.Fahrenheit)]
    [InlineData("garbage", TemperatureUnit.Fahrenheit)]
    [InlineData(null, TemperatureUnit.Fahrenheit)]
    public void UnitFrom_ReadsConfig(string? value, TemperatureUnit expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Units:Temperature"] = value }).Build();

        Assert.Equal(expected, TemperatureDisplay.UnitFrom(config));
    }
}
