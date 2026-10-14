namespace HomeSpeaker.Shared;

/// <summary>Body of <c>PUT /api/homespeaker/live-activities/{activityId}</c>.</summary>
public sealed class LiveActivityRegistrationRequest
{
    /// <summary>The Live Activity's ActivityKit push token, hex encoded.</summary>
    public string? PushToken { get; set; }
}
