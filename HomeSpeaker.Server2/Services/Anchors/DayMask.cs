namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Days of the week as a bitmask: Sunday = 1, Monday = 2, ... Saturday = 64.</summary>
public static class DayMask
{
    public const int Every = 127;

    public static bool Includes(int mask, DayOfWeek day) => (mask & (1 << (int)day)) != 0;

    public static int Of(params DayOfWeek[] days) => days.Aggregate(0, (mask, day) => mask | (1 << (int)day));

    /// <summary>Clamps any int into a valid non-empty mask; 0 or out-of-range bits mean every day.</summary>
    public static int Normalize(int? mask)
    {
        var value = (mask ?? Every) & Every;
        return value == 0 ? Every : value;
    }
}
