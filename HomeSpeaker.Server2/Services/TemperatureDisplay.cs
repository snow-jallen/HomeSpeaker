using System.Globalization;

namespace HomeSpeaker.Server2.Services;

/// <summary>The one place a stored Fahrenheit temperature becomes display text, in the configured unit.</summary>
public static class TemperatureDisplay
{
    public static TemperatureUnit UnitFrom(IConfiguration configuration) =>
        configuration["Units:Temperature"]?.Trim() is { } value &&
        (string.Equals(value, "C", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Celsius", StringComparison.OrdinalIgnoreCase))
            ? TemperatureUnit.Celsius
            : TemperatureUnit.Fahrenheit;

    public static double Convert(double fahrenheit, TemperatureUnit unit) =>
        unit == TemperatureUnit.Celsius ? (fahrenheit - 32) * 5 / 9 : fahrenheit;

    /// <summary>Converts a difference between two temperatures, which has no 32 degree offset.</summary>
    public static double ConvertDelta(double fahrenheitDelta, TemperatureUnit unit) =>
        unit == TemperatureUnit.Celsius ? fahrenheitDelta * 5 / 9 : fahrenheitDelta;

    /// <summary>Converts a value the user entered in <paramref name="unit"/> back to the stored Fahrenheit.</summary>
    public static double ToFahrenheit(double value, TemperatureUnit unit) =>
        unit == TemperatureUnit.Celsius ? value * 9 / 5 + 32 : value;

    /// <summary>Whole degrees with a degree sign and no unit letter ("72°"), or an en dash when there is no reading.</summary>
    public static string Format(double? fahrenheit, TemperatureUnit unit)
    {
        if (fahrenheit is not { } value)
        {
            return "–";
        }

        var rounded = Math.Round(Convert(value, unit), MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            rounded = 0; // A small negative rounds to -0, which would print as "-0°".
        }

        return string.Create(CultureInfo.InvariantCulture, $"{rounded:0}°");
    }
}
