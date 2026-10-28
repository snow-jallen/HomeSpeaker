using System.Globalization;

namespace HomeSpeaker.Server2.Components.Glass;

// Lives in code-behind because the Razor compiler (SDK 10.0.1xx) reads relational patterns like "< 60 =>" in @code as markup.
public sealed partial class AmbientOverlay
{
    // Hours past the first hour, so an old reading can't widen the fixed-size panel.
    private static string age(TimeSpan sinceReading) => sinceReading.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)sinceReading.TotalMinutes} min ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)sinceReading.TotalHours} hr ago"),
    };

    // 1 at 30 min, 2 at 45, 3 at an hour, 4 at two hours: amber through deep red.
    private static int ageLevel(TimeSpan sinceReading) => sinceReading.TotalMinutes switch
    {
        < 45 => 1,
        < 60 => 2,
        < 120 => 3,
        _ => 4,
    };

    // "42 min", "1h 15m", and whole hours past ten so the headline stays inside the panel.
    private static string longAge(TimeSpan sinceReading)
    {
        var minutes = (int)sinceReading.TotalMinutes;
        return minutes switch
        {
            < 60 => string.Create(CultureInfo.InvariantCulture, $"{minutes} min"),
            < 600 => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}h {minutes % 60}m"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} hr"),
        };
    }
}
