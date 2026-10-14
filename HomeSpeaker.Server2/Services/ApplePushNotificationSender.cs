using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class ApplePushNotificationSender : IPushNotificationSender, ILiveActivityPushSender, IDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly PushNotificationOptions options;
    private readonly ILogger<ApplePushNotificationSender> logger;
    private readonly TimeProvider timeProvider;
    private readonly ECDsa? signingKey;
    private readonly object jwtLock = new();
    private string? cachedJwt;
    private DateTimeOffset cachedJwtExpiresUtc;

    public ApplePushNotificationSender(
        IHttpClientFactory httpClientFactory,
        IOptions<PushNotificationOptions> options,
        ILogger<ApplePushNotificationSender> logger,
        TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.logger = logger;
        this.timeProvider = timeProvider;
        signingKey = loadSigningKey(this.options.Apns);
    }

    public Task<PushNotificationSendResult> SendAsync(
        PushNotificationPlatform platform,
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        if (platform != PushNotificationPlatform.Apns)
        {
            return Task.FromResult(new PushNotificationSendResult(false, ErrorCode: "unsupported-platform", ErrorMessage: $"Unsupported push platform '{platform}'."));
        }

        if (!options.CanSend || signingKey is null)
        {
            return Task.FromResult(notConfigured());
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            aps = new
            {
                alert = new
                {
                    title,
                    body
                },
                sound = "default"
            },
            data
        }, serializerOptions);

        // Collapse on the alert key so a newer state for the same monitor (e.g. blood sugar
        // going from "low" to "high") replaces the previous banner rather than stacking.
        data.TryGetValue("alertKey", out var alertKey);
        var message = new ApnsMessage(
            payloadJson,
            PushType: "alert",
            Topic: options.Apns.Topic!, // CanSend implies Apns.IsConfigured, so Topic is set.
            Priority: 10,
            CollapseId: string.IsNullOrWhiteSpace(alertKey) ? null : alertKey);

        return sendWithEnvironmentFallbackAsync(deviceToken, message, cancellationToken);
    }

    public Task<PushNotificationSendResult> SendLiveActivityAsync(
        string pushToken,
        string payloadJson,
        int priority,
        CancellationToken cancellationToken = default)
    {
        if (!options.CanSend || signingKey is null)
        {
            return Task.FromResult(notConfigured());
        }

        // ActivityKit pushes go to the app's topic with a ".push-type.liveactivity" suffix.
        var message = new ApnsMessage(
            payloadJson,
            PushType: "liveactivity",
            Topic: $"{options.Apns.Topic}.push-type.liveactivity",
            Priority: priority,
            CollapseId: null);

        return sendWithEnvironmentFallbackAsync(pushToken, message, cancellationToken);
    }

    private static PushNotificationSendResult notConfigured()
        => new(false, ErrorCode: "push-not-configured", ErrorMessage: "APNs is not configured.");

    private async Task<PushNotificationSendResult> sendWithEnvironmentFallbackAsync(
        string deviceToken,
        ApnsMessage message,
        CancellationToken cancellationToken)
    {
        var primarySandbox = options.Apns.UseSandbox;
        var primary = await sendToHostAsync(primarySandbox, deviceToken, message, cancellationToken);

        // A device token doesn't reveal which APNs environment it belongs to, and a token sent
        // to the wrong environment comes back as BadDeviceToken. So if the configured environment
        // rejects it that way, try the other one once before giving up on the device. This makes
        // delivery work regardless of whether the app was a development (Xcode) or production
        // (TestFlight/App Store) build — UseSandbox just decides which environment we try first.
        if (primary.Outcome == ApnsOutcome.WrongEnvironment)
        {
            logger.LogInformation(
                "APNs returned BadDeviceToken from the {Primary} environment; retrying {Secondary}.",
                environmentName(primarySandbox),
                environmentName(!primarySandbox));

            var secondary = await sendToHostAsync(!primarySandbox, deviceToken, message, cancellationToken);
            if (secondary.Outcome != ApnsOutcome.WrongEnvironment)
            {
                return secondary.Result;
            }

            logger.LogWarning("APNs rejected the device token as BadDeviceToken in both environments; deactivating device.");
            return new PushNotificationSendResult(
                false,
                ShouldDeactivateDevice: true,
                ErrorCode: "BadDeviceToken",
                ErrorMessage: "Device token is not valid in either APNs environment.");
        }

        return primary.Result;
    }

    private async Task<ApnsAttempt> sendToHostAsync(
        bool useSandbox,
        string deviceToken,
        ApnsMessage message,
        CancellationToken cancellationToken)
    {
        var environmentHost = useSandbox
            ? "https://api.sandbox.push.apple.com"
            : "https://api.push.apple.com";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{environmentHost}/3/device/{deviceToken}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Content = new StringContent(message.PayloadJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", getJwt());
        request.Headers.TryAddWithoutValidation("apns-topic", message.Topic);
        request.Headers.TryAddWithoutValidation("apns-push-type", message.PushType);
        request.Headers.TryAddWithoutValidation("apns-priority", message.Priority.ToString(CultureInfo.InvariantCulture));

        if (message.CollapseId is not null)
        {
            request.Headers.TryAddWithoutValidation("apns-collapse-id", message.CollapseId);
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApnsClient");
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new ApnsAttempt(ApnsOutcome.Success, new PushNotificationSendResult(true));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var apnsReason = tryGetApnsReason(responseBody);

            // BadDeviceToken is the environment-ambiguous case — let the caller retry the other one.
            if (string.Equals(apnsReason, "BadDeviceToken", StringComparison.OrdinalIgnoreCase))
            {
                return new ApnsAttempt(
                    ApnsOutcome.WrongEnvironment,
                    new PushNotificationSendResult(false, ErrorCode: apnsReason, ErrorMessage: "Device token is not valid for this APNs environment."));
            }

            // Unregistered (app uninstalled, or the Live Activity ended), DeviceTokenNotForTopic and
            // ExpiredToken are terminal for this token regardless of environment, so deactivate it
            // without retrying.
            if (string.Equals(apnsReason, "Unregistered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(apnsReason, "DeviceTokenNotForTopic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(apnsReason, "ExpiredToken", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("APNs rejected device token with reason {Reason}", apnsReason);
                return new ApnsAttempt(
                    ApnsOutcome.Terminal,
                    new PushNotificationSendResult(false, ShouldDeactivateDevice: true, ErrorCode: apnsReason, ErrorMessage: "Device token is no longer valid."));
            }

            logger.LogWarning("APNs push failed with status {StatusCode} and reason {Reason}", (int)response.StatusCode, apnsReason ?? "unknown");
            return new ApnsAttempt(
                ApnsOutcome.Transient,
                new PushNotificationSendResult(false, ErrorCode: apnsReason ?? response.StatusCode.ToString(), ErrorMessage: responseBody));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "APNs push request failed");
            return new ApnsAttempt(
                ApnsOutcome.Transient,
                new PushNotificationSendResult(false, ErrorCode: "apns-request-failed", ErrorMessage: ex.Message));
        }
    }

    private static string environmentName(bool useSandbox) => useSandbox ? "sandbox" : "production";

    private enum ApnsOutcome
    {
        Success,
        WrongEnvironment,
        Terminal,
        Transient
    }

    private sealed record ApnsAttempt(ApnsOutcome Outcome, PushNotificationSendResult Result);

    private sealed record ApnsMessage(string PayloadJson, string PushType, string Topic, int Priority, string? CollapseId);

    public void Dispose() => signingKey?.Dispose();

    private string getJwt()
    {
        var now = timeProvider.GetUtcNow();
        lock (jwtLock)
        {
            if (!string.IsNullOrWhiteSpace(cachedJwt) && cachedJwtExpiresUtc > now.AddMinutes(5))
            {
                return cachedJwt;
            }

            cachedJwt = createJwt(now);
            cachedJwtExpiresUtc = now.AddMinutes(50);
            return cachedJwt;
        }
    }

    private string createJwt(DateTimeOffset issuedAtUtc)
    {
        if (signingKey is null)
        {
            throw new InvalidOperationException("APNs signing key is not available.");
        }

        var header = base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "ES256",
            kid = options.Apns.KeyId
        }));

        var payload = base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = options.Apns.TeamId,
            iat = issuedAtUtc.ToUnixTimeSeconds()
        }));

        var unsignedToken = $"{header}.{payload}";
        var signature = signingKey.SignData(Encoding.UTF8.GetBytes(unsignedToken), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{unsignedToken}.{base64UrlEncode(signature)}";
    }

    private static ECDsa? loadSigningKey(PushNotificationOptions.ApnsOptions options)
    {
        if (!options.TryGetPrivateKeyPem(out var pem))
        {
            return null;
        }

        var key = ECDsa.Create();
        key.ImportFromPem(pem);
        return key;
    }

    private static string? tryGetApnsReason(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
