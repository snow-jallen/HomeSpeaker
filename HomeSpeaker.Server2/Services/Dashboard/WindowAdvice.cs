using System.Globalization;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>
/// One line of window advice for the Home climate card. It follows the flag the temperature service computes
/// (<see cref="TemperatureStatus.ShouldWindowsBeClosed"/>: close when outside is as warm as the younger girls room
/// or below 50 degrees) and words the reason from the two temperatures.
/// </summary>
public sealed record WindowAdvice(bool Close, string Text)
{
    /// <summary>Null when the outside or younger girls room reading is missing.</summary>
    public static WindowAdvice? From(TemperatureStatus? status)
    {
        if (status?.OutsideTemperature is not { } outside || status.YoungerGirlsRoomTemperature is not { } inside)
        {
            return null;
        }

        var degrees = (int)Math.Round(status.TemperatureDifference, MidpointRounding.AwayFromZero);
        var amount = degrees > 0 ? string.Create(CultureInfo.InvariantCulture, $"{degrees}° ") : "";

        if (!status.ShouldWindowsBeClosed)
        {
            return new WindowAdvice(false, $"Open windows · {amount}cooler outside");
        }

        var warmer = outside >= inside;
        return new WindowAdvice(true, $"Close windows · {amount}{(warmer ? "warmer" : "colder")} outside");
    }
}
