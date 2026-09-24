using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

public interface IPushNotificationSender
{
    Task<PushNotificationSendResult> SendAsync(
        PushNotificationPlatform platform,
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}

public sealed record PushNotificationSendResult(
    bool IsSuccess,
    bool ShouldDeactivateDevice = false,
    string? ErrorCode = null,
    string? ErrorMessage = null);
