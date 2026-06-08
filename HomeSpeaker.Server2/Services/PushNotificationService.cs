using System.Text.RegularExpressions;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using HomeSpeaker.Shared.BloodSugar;
using HomeSpeaker.Shared.Temperature;
using HomeSpeaker.Shared.WindowMonitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services;

public sealed class PushNotificationService
{
    private const string TemperatureAlertKey = "temperature-window-state";
    private const string BloodSugarAlertKey = "blood-sugar-status";
    private static readonly Regex apnsTokenRegex = new("^[0-9a-fA-F]+$", RegexOptions.Compiled);

    private readonly MusicContext dbContext;
    private readonly IPushNotificationSender sender;
    private readonly PushNotificationOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PushNotificationService> logger;

    public PushNotificationService(
        MusicContext dbContext,
        IPushNotificationSender sender,
        IOptions<PushNotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<PushNotificationService> logger)
    {
        this.dbContext = dbContext;
        this.sender = sender;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<PushDeviceRegistrationDto> RegisterDeviceAsync(PushDeviceRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        validateRequest(request);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var normalizedToken = normalizeDeviceToken(request.DeviceToken);
        var normalizedInstallationId = request.InstallationId.Trim();
        var normalizedDeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? null : request.DeviceName.Trim();

        await retireOtherTokenOwnersAsync(normalizedToken, normalizedInstallationId, cancellationToken);

        var entity = await dbContext.PushNotificationDevices
            .SingleOrDefaultAsync(device => device.InstallationId == normalizedInstallationId, cancellationToken);

        if (entity is null)
        {
            entity = new PushNotificationDevice
            {
                InstallationId = normalizedInstallationId,
                RegisteredUtc = now,
                DeviceTokenUpdatedUtc = now
            };
            dbContext.PushNotificationDevices.Add(entity);
        }

        var tokenChanged = !string.Equals(entity.DeviceToken, normalizedToken, StringComparison.Ordinal);
        entity.InstallationId = normalizedInstallationId;
        entity.Platform = request.Platform;
        entity.DeviceToken = normalizedToken;
        entity.DeviceName = normalizedDeviceName;
        entity.TemperatureAlertsEnabled = request.TemperatureAlertsEnabled;
        entity.BloodSugarAlertsEnabled = request.BloodSugarAlertsEnabled;
        entity.IsActive = true;
        entity.UpdatedUtc = now;
        if (tokenChanged || entity.DeviceTokenUpdatedUtc is null)
        {
            entity.DeviceTokenUpdatedUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return map(entity);
    }

    public async Task<PushRegistrationStatusDto> GetRegistrationStatusAsync(string installationId, CancellationToken cancellationToken = default)
    {
        var normalizedInstallationId = normalizeInstallationId(installationId);
        var entity = await dbContext.PushNotificationDevices
            .AsNoTracking()
            .SingleOrDefaultAsync(device => device.InstallationId == normalizedInstallationId, cancellationToken);

        return mapRegistrationStatus(normalizedInstallationId, entity);
    }

    public async Task<PushRegistrationStatusDto> UpsertInstallationAsync(UpsertPushInstallationRequest request, CancellationToken cancellationToken = default)
    {
        validateInstallationRequest(request);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var normalizedInstallationId = normalizeInstallationId(request.InstallationId);
        var normalizedToken = normalizeDeviceToken(request.DeviceToken);
        var normalizedDeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? null : request.DeviceName.Trim();
        var normalizedBundleId = string.IsNullOrWhiteSpace(request.BundleId) ? null : request.BundleId.Trim();

        await retireOtherTokenOwnersAsync(normalizedToken, normalizedInstallationId, cancellationToken);

        var entity = await dbContext.PushNotificationDevices
            .SingleOrDefaultAsync(device => device.InstallationId == normalizedInstallationId, cancellationToken);

        if (entity is null)
        {
            entity = new PushNotificationDevice
            {
                InstallationId = normalizedInstallationId,
                RegisteredUtc = now,
                DeviceTokenUpdatedUtc = now
            };
            dbContext.PushNotificationDevices.Add(entity);
        }

        var tokenChanged = !string.Equals(entity.DeviceToken, normalizedToken, StringComparison.Ordinal);
        entity.InstallationId = normalizedInstallationId;
        entity.Platform = parsePlatform(request.Platform);
        entity.DeviceToken = normalizedToken;
        entity.DeviceName = normalizedDeviceName;
        entity.BundleId = normalizedBundleId;
        entity.TemperatureAlertsEnabled = request.TemperatureAlertsEnabled;
        entity.BloodSugarAlertsEnabled = request.BloodSugarAlertsEnabled;
        entity.IsActive = true;
        entity.UpdatedUtc = now;
        if (tokenChanged || entity.DeviceTokenUpdatedUtc is null)
        {
            entity.DeviceTokenUpdatedUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return mapRegistrationStatus(normalizedInstallationId, entity);
    }

    public async Task<bool> UnregisterDeviceAsync(string installationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installationId))
        {
            return false;
        }

        var entity = await dbContext.PushNotificationDevices
            .SingleOrDefaultAsync(device => device.InstallationId == installationId.Trim(), cancellationToken);

        if (entity is null)
        {
            return false;
        }

        entity.IsActive = false;
        entity.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task NotifyTemperatureStatusAsync(TemperatureStatus status, CancellationToken cancellationToken = default)
        => notifyAsync(buildTemperatureAlert(status), device => device.TemperatureAlertsEnabled, cancellationToken);

    public Task NotifyBloodSugarStatusAsync(BloodSugarStatus status, CancellationToken cancellationToken = default)
        => notifyAsync(buildBloodSugarAlert(status), device => device.BloodSugarAlertsEnabled, cancellationToken);

    private async Task notifyAsync(PushAlertDefinition? alert, Func<PushNotificationDevice, bool> isEligible, CancellationToken cancellationToken)
    {
        if (alert is null)
        {
            return;
        }

        if (alert.IsCleared)
        {
            await clearAlertStateAsync(alert.AlertKey, cancellationToken);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var repeatWindow = TimeSpan.FromMinutes(Math.Max(1, options.RepeatAlertMinutes));
        var state = await dbContext.PushNotificationAlertStates.SingleOrDefaultAsync(item => item.AlertKey == alert.AlertKey, cancellationToken);
        if (state is not null &&
            string.Equals(state.ActiveStateKey, alert.StateKey, StringComparison.Ordinal) &&
            state.LastSentUtc is { } lastSentUtc &&
            now - lastSentUtc < repeatWindow)
        {
            return;
        }

        var devices = await dbContext.PushNotificationDevices
            .Where(device => device.IsActive)
            .ToListAsync(cancellationToken);
        devices = devices.Where(isEligible).ToList();

        if (devices.Count == 0)
        {
            return;
        }

        var anySuccess = false;
        foreach (var device in devices)
        {
            var result = await sender.SendAsync(
                device.Platform,
                device.DeviceToken,
                alert.Title,
                alert.Body,
                new Dictionary<string, string>
                {
                    ["alertKey"] = alert.AlertKey,
                    ["stateKey"] = alert.StateKey
                },
                cancellationToken);

            device.LastNotificationAttemptUtc = now;
            if (result.IsSuccess)
            {
                anySuccess = true;
                device.LastSuccessfulNotificationUtc = now;
            }
            else if (result.ShouldDeactivateDevice)
            {
                device.IsActive = false;
                device.UpdatedUtc = now;
            }
        }

        if (anySuccess)
        {
            state ??= new PushNotificationAlertState
            {
                AlertKey = alert.AlertKey
            };

            if (dbContext.Entry(state).State == EntityState.Detached)
            {
                dbContext.PushNotificationAlertStates.Add(state);
            }

            state.ActiveStateKey = alert.StateKey;
            state.LastSentUtc = now;
            state.UpdatedUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task retireOtherTokenOwnersAsync(string normalizedToken, string normalizedInstallationId, CancellationToken cancellationToken)
    {
        var staleOwners = await dbContext.PushNotificationDevices
            .Where(device => device.DeviceToken == normalizedToken && device.InstallationId != normalizedInstallationId)
            .ToListAsync(cancellationToken);

        if (staleOwners.Count == 0)
        {
            return;
        }

        // An APNs device token belongs to exactly one app installation at a time. If a
        // different installation still claims this token it is stale (reinstall, restored
        // backup, or Apple reassigned the token), so drop it. Saving here — before the
        // token is assigned to the current device — keeps SQLite's unique index on
        // DeviceToken from tripping mid-transaction.
        logger.LogInformation(
            "Retiring {Count} stale push device(s) that previously held this device token before reassigning it to installation {InstallationId}.",
            staleOwners.Count,
            normalizedInstallationId);
        dbContext.PushNotificationDevices.RemoveRange(staleOwners);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task clearAlertStateAsync(string alertKey, CancellationToken cancellationToken)
    {
        var state = await dbContext.PushNotificationAlertStates.SingleOrDefaultAsync(item => item.AlertKey == alertKey, cancellationToken);
        if (state is null)
        {
            return;
        }

        state.ActiveStateKey = null;
        state.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PushAlertDefinition? buildTemperatureAlert(TemperatureStatus status)
    {
        if (status.OutsideTemperature is not { } outsideTemp || status.YoungerGirlsRoomTemperature is not { } girlsRoomTemp)
        {
            return new PushAlertDefinition(TemperatureAlertKey, string.Empty, string.Empty, string.Empty, IsCleared: true);
        }

        var keepClosed = status.ShouldWindowsBeClosed;
        var title = keepClosed ? "Keep the windows closed" : "Open the windows";
        var body = $"Outside {outsideTemp:F0}°, girl's room {girlsRoomTemp:F0}° (Δ {status.TemperatureDifference:F0}°).";
        return new PushAlertDefinition(
            TemperatureAlertKey,
            keepClosed ? "keep-closed" : "open-windows",
            title,
            body);
    }

    private static PushAlertDefinition buildBloodSugarAlert(BloodSugarStatus status)
    {
        if (status.CurrentReading?.Sgv is not { } sgv)
        {
            return status.IsStale
                ? new PushAlertDefinition(BloodSugarAlertKey, "no-data", "Blood sugar data unavailable", "HomeSpeaker could not load a recent blood sugar reading.")
                : new PushAlertDefinition(BloodSugarAlertKey, string.Empty, string.Empty, string.Empty, IsCleared: true);
        }

        if (status.IsStale)
        {
            return new PushAlertDefinition(
                BloodSugarAlertKey,
                "stale",
                "Blood sugar reading is stale",
                $"Last reading was {formatAge(status.TimeSinceLastReading)} ago.");
        }

        var stateKey = sgv switch
        {
            < 70 => "low",
            < 80 => "below-target",
            <= 180 => string.Empty,
            <= 250 => "above-target",
            _ => "high"
        };

        if (string.IsNullOrEmpty(stateKey))
        {
            return new PushAlertDefinition(BloodSugarAlertKey, string.Empty, string.Empty, string.Empty, IsCleared: true);
        }

        var title = stateKey switch
        {
            "low" => "Blood sugar is low",
            "below-target" => "Blood sugar is below target",
            "above-target" => "Blood sugar is above target",
            _ => "Blood sugar is high"
        };

        var trend = string.IsNullOrWhiteSpace(status.CurrentReading.DirectionDescription)
            ? "trend unavailable"
            : status.CurrentReading.DirectionDescription;

        return new PushAlertDefinition(
            BloodSugarAlertKey,
            stateKey,
            title,
            $"{sgv:F0} mg/dL, {trend}, {formatAge(status.TimeSinceLastReading)} ago.");
    }

    private static string formatAge(TimeSpan age)
    {
        if (age.TotalHours >= 1)
        {
            return $"{Math.Floor(age.TotalHours)}h {age.Minutes}m";
        }

        return $"{Math.Max(0, age.Minutes)}m";
    }

    private static string normalizeDeviceToken(string token)
        => token.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string normalizeInstallationId(string installationId)
    {
        if (string.IsNullOrWhiteSpace(installationId) || installationId.Trim().Length > 200)
        {
            throw new ArgumentException("InstallationId is required and must be 200 characters or fewer.", nameof(installationId));
        }

        return installationId.Trim();
    }

    private static void validateRequest(PushDeviceRegistrationRequest request)
    {
        _ = normalizeInstallationId(request.InstallationId);

        var normalizedToken = normalizeDeviceToken(request.DeviceToken);
        if (normalizedToken.Length < 32 || normalizedToken.Length > 512 || !apnsTokenRegex.IsMatch(normalizedToken))
        {
            throw new ArgumentException("DeviceToken must be a valid APNs token.", nameof(request));
        }

        if (request.DeviceName?.Length > 200)
        {
            throw new ArgumentException("DeviceName must be 200 characters or fewer.", nameof(request));
        }
    }

    private static void validateInstallationRequest(UpsertPushInstallationRequest request)
    {
        _ = normalizeInstallationId(request.InstallationId);

        var normalizedToken = normalizeDeviceToken(request.DeviceToken);
        if (normalizedToken.Length < 32 || normalizedToken.Length > 512 || !apnsTokenRegex.IsMatch(normalizedToken))
        {
            throw new ArgumentException("DeviceToken must be a valid APNs token.", nameof(request));
        }

        if (request.DeviceName?.Length > 200)
        {
            throw new ArgumentException("DeviceName must be 200 characters or fewer.", nameof(request));
        }

        if (request.BundleId?.Length > 200)
        {
            throw new ArgumentException("BundleId must be 200 characters or fewer.", nameof(request));
        }

        _ = parsePlatform(request.Platform);
    }

    private static PushNotificationPlatform parsePlatform(string? platform) =>
        platform?.Trim().ToLowerInvariant() switch
        {
            "apns" or "ios" => PushNotificationPlatform.Apns,
            _ => throw new ArgumentException("Platform must be 'ios' or 'apns'.", nameof(platform))
        };

    private static PushDeviceRegistrationDto map(PushNotificationDevice entity) => new()
    {
        InstallationId = entity.InstallationId,
        Platform = entity.Platform,
        DeviceName = entity.DeviceName,
        TemperatureAlertsEnabled = entity.TemperatureAlertsEnabled,
        BloodSugarAlertsEnabled = entity.BloodSugarAlertsEnabled,
        IsActive = entity.IsActive,
        RegisteredUtc = entity.RegisteredUtc,
        UpdatedUtc = entity.UpdatedUtc
    };

    private static PushRegistrationStatusDto mapRegistrationStatus(string installationId, PushNotificationDevice? entity) => new()
    {
        InstallationId = installationId,
        Platform = entity is null ? null : mapPlatform(entity.Platform),
        IsRegistered = entity?.IsActive == true,
        BundleId = entity?.BundleId,
        DeviceTokenUpdatedUtc = entity?.DeviceTokenUpdatedUtc,
        TemperatureAlertsEnabled = entity?.IsActive == true && entity.TemperatureAlertsEnabled,
        BloodSugarAlertsEnabled = entity?.IsActive == true && entity.BloodSugarAlertsEnabled
    };

    private static string mapPlatform(PushNotificationPlatform platform) =>
        platform switch
        {
            PushNotificationPlatform.Apns => "ios",
            _ => "ios"
        };

    private sealed record PushAlertDefinition(
        string AlertKey,
        string StateKey,
        string Title,
        string Body,
        bool IsCleared = false);
}
