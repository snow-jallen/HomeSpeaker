namespace HomeSpeaker.Server2.Services;

/// <summary>Sends ActivityKit (Live Activity) pushes through APNs.</summary>
public interface ILiveActivityPushSender
{
    /// <param name="pushToken">The Live Activity's own push token (not the device token).</param>
    /// <param name="payloadJson">The full APNs payload, including the <c>aps</c> envelope.</param>
    /// <param name="priority">APNs priority: 10 delivers immediately, 5 lets iOS budget and coalesce.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<PushNotificationSendResult> SendLiveActivityAsync(
        string pushToken,
        string payloadJson,
        int priority,
        CancellationToken cancellationToken = default);
}
