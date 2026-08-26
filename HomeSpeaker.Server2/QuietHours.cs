namespace HomeSpeaker.Server2;

/// <summary>
/// The overnight quiet window, shared by the sleepy-time screen overlay and the
/// autoplay monitor so "screen dims" and "autoplay stops" always agree.
/// The window wraps midnight: 22:00 through 06:30 local time.
/// </summary>
public static class QuietHours
{
    public static readonly TimeSpan Start = new(22, 0, 0);
    public static readonly TimeSpan End = new(6, 30, 0);

    public static bool IsQuietTime(TimeProvider timeProvider)
    {
        var now = timeProvider.GetLocalNow().TimeOfDay;
        return now >= Start || now < End;
    }
}
