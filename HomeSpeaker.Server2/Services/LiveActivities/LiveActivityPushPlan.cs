namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>A push the dispatcher should send now: its kind and APNs priority (10 or 5).</summary>
public sealed record LiveActivityPushPlan(LiveActivityPushKind Kind, int Priority)
{
    public const int HighPriority = 10;
    public const int LowPriority = 5;
}
