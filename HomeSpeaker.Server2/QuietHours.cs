namespace HomeSpeaker.Server2;

/// <summary>
/// The overnight quiet window, shared by the sleepy-time screen overlay, the
/// autoplay monitor and the resume-on-restart in <see cref="LifecycleEvents"/>
/// so "screen dims", "autoplay stops" and "a restart stays silent" always agree.
/// The window wraps midnight: 22:00 through 07:00 local time.
/// </summary>
public static class QuietHours
{
    public static readonly TimeSpan Start = new(22, 0, 0);
    public static readonly TimeSpan End = new(7, 0, 0);

    public static bool IsQuietTime(TimeProvider timeProvider)
    {
        var now = timeProvider.GetLocalNow().TimeOfDay;
        return now >= Start || now < End;
    }
}
