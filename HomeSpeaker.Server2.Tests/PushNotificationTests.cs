using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using HomeSpeaker.Server2;
using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using HomeSpeaker.Shared.Temperature;
using HomeSpeaker.Shared.WindowMonitoring;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HomeSpeaker.Server2.Tests;

public sealed class PushNotificationTests : IAsyncLifetime
{
    private readonly PushWebApplicationFactory factory = new();
    private HttpClient httpClient = null!;

    public async Task InitializeAsync()
    {
        httpClient = factory.CreateClient();
        await factory.InitializeDatabaseAsync();
        factory.Sender.Reset();
    }

    public async Task DisposeAsync()
    {
        httpClient.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task UpsertingInstallationReturnsRegisteredStatusWithAlertFlags()
    {
        var response = await httpClient.PutAsJsonAsync(
            "/api/homespeaker/push/installations/zoe-phone",
            new UpsertPushInstallationRequest
            {
                InstallationId = "ignored-by-route",
                Platform = "ios",
                DeviceToken = "<0123456789abcdef0123456789abcdef>",
                DeviceName = "Zoe's iPhone",
                BundleId = "com.homespeaker.mobile",
                TemperatureAlertsEnabled = true,
                BloodSugarAlertsEnabled = false
            });

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<PushRegistrationStatusDto>();

        Assert.NotNull(status);
        Assert.Equal("zoe-phone", status!.InstallationId);
        Assert.Equal("ios", status.Platform);
        Assert.True(status.IsRegistered);
        Assert.Equal("com.homespeaker.mobile", status.BundleId);
        Assert.NotNull(status.DeviceTokenUpdatedUtc);
        Assert.True(status.TemperatureAlertsEnabled);
        Assert.False(status.BloodSugarAlertsEnabled);
    }

    [Fact]
    public async Task ReupsertingInstallationPersistsAlertPreferences()
    {
        await upsertInstallationAsync("zoe-phone", temperatureAlertsEnabled: false, bloodSugarAlertsEnabled: true);

        var updateResponse = await httpClient.PutAsJsonAsync(
            "/api/homespeaker/push/installations/zoe-phone",
            new UpsertPushInstallationRequest
            {
                InstallationId = "ignored-by-route",
                Platform = "ios",
                DeviceToken = "0123456789abcdef0123456789abcdef",
                DeviceName = "Zoe's iPhone",
                BundleId = "com.homespeaker.mobile",
                TemperatureAlertsEnabled = true,
                BloodSugarAlertsEnabled = false
            });

        updateResponse.EnsureSuccessStatusCode();
        var updatedStatus = await updateResponse.Content.ReadFromJsonAsync<PushRegistrationStatusDto>();
        Assert.NotNull(updatedStatus);
        Assert.True(updatedStatus!.TemperatureAlertsEnabled);
        Assert.False(updatedStatus.BloodSugarAlertsEnabled);

        var refreshedStatus = await httpClient.GetFromJsonAsync<PushRegistrationStatusDto>("/api/homespeaker/push/installations/zoe-phone");
        Assert.NotNull(refreshedStatus);
        Assert.True(refreshedStatus!.TemperatureAlertsEnabled);
        Assert.False(refreshedStatus.BloodSugarAlertsEnabled);
    }

    [Fact]
    public async Task UnregisteringDeviceMarksRegistrationInactive()
    {
        await upsertInstallationAsync("zoe-phone", temperatureAlertsEnabled: true, bloodSugarAlertsEnabled: true);

        var response = await httpClient.DeleteAsync("/api/push/devices/zoe-phone");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refreshedStatus = await httpClient.GetFromJsonAsync<PushRegistrationStatusDto>("/api/homespeaker/push/installations/zoe-phone");
        Assert.NotNull(refreshedStatus);
        Assert.False(refreshedStatus!.IsRegistered);
        Assert.False(refreshedStatus.TemperatureAlertsEnabled);
        Assert.False(refreshedStatus.BloodSugarAlertsEnabled);
    }

    [Fact]
    public async Task TemperatureAlertsSendOncePerStateUntilCleared()
    {
        await upsertInstallationAsync("zoe-phone", temperatureAlertsEnabled: true);

        using var scope = factory.Services.CreateScope();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<PushNotificationService>();
        var alertStatus = new TemperatureStatus
        {
            OutsideTemperature = 42,
            YoungerGirlsRoomTemperature = 73,
            ShouldWindowsBeClosed = true,
            TemperatureDifference = -31
        };

        await pushNotificationService.NotifyTemperatureStatusAsync(alertStatus);
        await pushNotificationService.NotifyTemperatureStatusAsync(alertStatus);

        var firstNotification = Assert.Single(factory.Sender.Notifications);
        Assert.Equal(PushNotificationPlatform.Apns, firstNotification.Platform);
        Assert.Equal("0123456789abcdef0123456789abcdef", firstNotification.DeviceToken);
        Assert.Equal("Keep the windows closed", firstNotification.Title);
        Assert.Equal("temperature-window-state", firstNotification.Data["alertKey"]);
        Assert.Equal("keep-closed", firstNotification.Data["stateKey"]);

        await pushNotificationService.NotifyTemperatureStatusAsync(new TemperatureStatus());
        await pushNotificationService.NotifyTemperatureStatusAsync(alertStatus);

        Assert.Equal(2, factory.Sender.Notifications.Count);
    }

    private async Task upsertInstallationAsync(string installationId, bool temperatureAlertsEnabled = false, bool bloodSugarAlertsEnabled = false)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/homespeaker/push/installations/{installationId}",
            new UpsertPushInstallationRequest
            {
                InstallationId = installationId,
                Platform = "ios",
                DeviceToken = "0123456789abcdef0123456789abcdef",
                DeviceName = "Zoe's iPhone",
                BundleId = "com.homespeaker.mobile",
                TemperatureAlertsEnabled = temperatureAlertsEnabled,
                BloodSugarAlertsEnabled = bloodSugarAlertsEnabled
            });
        response.EnsureSuccessStatusCode();
    }

    private sealed class PushWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string artifactsPath;
        private readonly string dbPath;
        private readonly string mediaFolderPath;

        public PushWebApplicationFactory()
        {
            artifactsPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "artifacts",
                "qa-push-notifications",
                Guid.NewGuid().ToString("N")));
            dbPath = Path.Combine(artifactsPath, "push-test.db");
            mediaFolderPath = Path.Combine(artifactsPath, "media");
            Directory.CreateDirectory(mediaFolderPath);
        }

        public FakePushNotificationSender Sender { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ConfigKeys.MediaFolder] = mediaFolderPath,
                    ["SqliteConnectionString"] = $"Data Source={dbPath}",
                    ["PushNotifications:Enabled"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                var hostedServices = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                        && descriptor.ImplementationType?.Assembly == typeof(Program).Assembly)
                    .ToList();

                foreach (var hostedService in hostedServices)
                {
                    services.Remove(hostedService);
                }

                var senderDescriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IPushNotificationSender) || descriptor.ServiceType == typeof(FakePushNotificationSender))
                    .ToList();

                foreach (var senderDescriptor in senderDescriptors)
                {
                    services.Remove(senderDescriptor);
                }

                services.AddSingleton(Sender);
                services.AddSingleton<IPushNotificationSender>(Sender);
            });
        }

        public async Task InitializeDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MusicContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();

            if (Directory.Exists(artifactsPath))
            {
                try
                {
                    Directory.Delete(artifactsPath, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    public sealed class FakePushNotificationSender : IPushNotificationSender
    {
        private readonly ConcurrentQueue<NotificationRecord> notificationQueue = new();

        public IReadOnlyCollection<NotificationRecord> Notifications => notificationQueue.ToArray();

        public Task<PushNotificationSendResult> SendAsync(
            PushNotificationPlatform platform,
            string deviceToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken = default)
        {
            notificationQueue.Enqueue(new NotificationRecord(platform, deviceToken, title, body, new Dictionary<string, string>(data)));
            return Task.FromResult(new PushNotificationSendResult(true));
        }

        public void Reset()
        {
            while (notificationQueue.TryDequeue(out _))
            {
            }
        }
    }

    public sealed record NotificationRecord(
        PushNotificationPlatform Platform,
        string DeviceToken,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string> Data);
}
