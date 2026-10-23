using System.Globalization;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// One line of window advice for the Home climate card. It follows the flag the temperature service computes
/// (<see cref="TemperatureStatus.ShouldWindowsBeClosed"/>: close when outside is as warm as the indoor room
/// or below the configured minimum) and words the reason from the two temperatures.
/// </summary>
public sealed record WindowAdvice(bool Close, string Text)
{
    /// <summary>Null when the outside or indoor room reading is missing (or window advice is off).</summary>
    public static WindowAdvice? From(TemperatureStatus? status, TemperatureUnit unit = TemperatureUnit.Fahrenheit)
    {
        if (status?.OutsideTemperature is not { } outside || status.IndoorTemperature is not { } inside)
        {
            return null;
        }

        var degrees = (int)Math.Round(TemperatureDisplay.ConvertDelta(status.TemperatureDifference, unit), MidpointRounding.AwayFromZero);
        var amount = degrees > 0 ? string.Create(CultureInfo.InvariantCulture, $"{degrees}° ") : "";

        if (!status.ShouldWindowsBeClosed)
        {
            return new WindowAdvice(false, $"Open windows · {amount}cooler outside");
        }

        if (degrees == 0)
        {
            return new WindowAdvice(true, "Close windows · same as inside");
        }

        var warmer = outside >= inside;
        return new WindowAdvice(true, $"Close windows · {amount}{(warmer ? "warmer" : "colder")} outside");
    }
}
