using HomeSpeaker.Server2.Data;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using HomeSpeaker.Shared.BloodSugar;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests;

public sealed class PushNotificationServiceTests
{
    [Fact]
    public async Task RegisterDeviceAsync_UpsertsExistingDeviceByInstallationId()
    {
        await using var fixture = await PushNotificationTestFixture.CreateAsync();
        var service = fixture.CreateService();

        await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "ios-main-phone",
            DeviceToken = new string('a', 64),
            DeviceName = "Jonathan's iPhone"
        });

        var updated = await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "ios-main-phone",
            DeviceToken = new string('b', 64),
            DeviceName = "Updated iPhone",
            TemperatureAlertsEnabled = false,
            BloodSugarAlertsEnabled = true
        });

        var devices = await fixture.DbContext.PushNotificationDevices.ToListAsync();
        Assert.Single(devices);
        Assert.Equal(new string('b', 64), devices[0].DeviceToken);
        Assert.Equal("Updated iPhone", updated.DeviceName);
        Assert.False(updated.TemperatureAlertsEnabled);
    }

    [Fact]
    public async Task NotifyTemperatureStatusAsync_SendsOnStateChangeButNotDuplicatePoll()
    {
        await using var fixture = await PushNotificationTestFixture.CreateAsync();
        var service = fixture.CreateService();

        await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "temperature-device",
            DeviceToken = new string('c', 64)
        });

        var openWindows = new TemperatureStatus
        {
            OutsideTemperature = 62,
            YoungerGirlsRoomTemperature = 71,
            TemperatureDifference = 9,
            ShouldWindowsBeClosed = false
        };

        await service.NotifyTemperatureStatusAsync(openWindows);
        await service.NotifyTemperatureStatusAsync(openWindows);

        Assert.Single(fixture.Sender.SentNotifications);
        Assert.Equal("open-windows", fixture.Sender.SentNotifications[0].Data["stateKey"]);

        var keepClosed = new TemperatureStatus
        {
            OutsideTemperature = 75,
            YoungerGirlsRoomTemperature = 71,
            TemperatureDifference = 4,
            ShouldWindowsBeClosed = true
        };

        await service.NotifyTemperatureStatusAsync(keepClosed);

        Assert.Equal(2, fixture.Sender.SentNotifications.Count);
        Assert.Equal("keep-closed", fixture.Sender.SentNotifications[1].Data["stateKey"]);
    }

    [Fact]
    public async Task NotifyTemperatureStatusAsync_OnlySendsToDevicesWithTemperatureAlertsEnabled()
    {
        await using var fixture = await PushNotificationTestFixture.CreateAsync();
        var service = fixture.CreateService();

        await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "temperature-device-disabled",
            DeviceToken = new string('d', 64),
            TemperatureAlertsEnabled = false,
            BloodSugarAlertsEnabled = true
        });

        await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "temperature-device-enabled",
            DeviceToken = new string('e', 64),
            TemperatureAlertsEnabled = true,
            BloodSugarAlertsEnabled = false
        });

        await service.NotifyTemperatureStatusAsync(new TemperatureStatus
        {
            OutsideTemperature = 62,
            YoungerGirlsRoomTemperature = 71,
            TemperatureDifference = 9,
            ShouldWindowsBeClosed = false
        });

        var notification = Assert.Single(fixture.Sender.SentNotifications);
        Assert.Equal(new string('e', 64), notification.DeviceToken);
        Assert.Equal("open-windows", notification.Data["stateKey"]);
    }

    [Fact]
    public async Task NotifyBloodSugarStatusAsync_ClearsInRangeStateAndAllowsFutureAlert()
    {
        await using var fixture = await PushNotificationTestFixture.CreateAsync();
        var service = fixture.CreateService();

        await service.RegisterDeviceAsync(new PushDeviceRegistrationRequest
        {
            InstallationId = "blood-sugar-device",
            DeviceToken = new string('f', 64),
            TemperatureAlertsEnabled = false,
            BloodSugarAlertsEnabled = true
        });

        await service.NotifyBloodSugarStatusAsync(new BloodSugarStatus
        {
            CurrentReading = new BloodSugarReading { Sgv = 63, DateString = DateTime.UtcNow.AddMinutes(-3), Direction = "SingleDown" },
            TimeSinceLastReading = TimeSpan.FromMinutes(3),
            LastUpdated = DateTime.UtcNow,
            IsStale = false
        });

        await service.NotifyBloodSugarStatusAsync(new BloodSugarStatus
        {
            CurrentReading = new BloodSugarReading { Sgv = 110, DateString = DateTime.UtcNow.AddMinutes(-2), Direction = "Flat" },
            TimeSinceLastReading = TimeSpan.FromMinutes(2),
            LastUpdated = DateTime.UtcNow,
            IsStale = false
        });

        await service.NotifyBloodSugarStatusAsync(new BloodSugarStatus
        {
            CurrentReading = new BloodSugarReading { Sgv = 255, DateString = DateTime.UtcNow.AddMinutes(-1), Direction = "SingleUp" },
            TimeSinceLastReading = TimeSpan.FromMinutes(1),
            LastUpdated = DateTime.UtcNow,
            IsStale = false
        });

        Assert.Equal(2, fixture.Sender.SentNotifications.Count);
        Assert.Equal("low", fixture.Sender.SentNotifications[0].Data["stateKey"]);
        Assert.Equal("high", fixture.Sender.SentNotifications[1].Data["stateKey"]);
    }

    private sealed class PushNotificationTestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public MusicContext DbContext { get; }
        public FakePushNotificationSender Sender { get; } = new();
        public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.Parse("2026-05-15T12:00:00Z"));

        private PushNotificationTestFixture(SqliteConnection connection, MusicContext dbContext)
        {
            this.connection = connection;
            DbContext = dbContext;
        }

        public static async Task<PushNotificationTestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MusicContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new MusicContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new PushNotificationTestFixture(connection, dbContext);
        }

        public PushNotificationService CreateService()
            => new(
                DbContext,
                Sender,
                Options.Create(new PushNotificationOptions
                {
                    Enabled = true,
                    RepeatAlertMinutes = 30
                }),
                TimeProvider,
                NullLogger<PushNotificationService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakePushNotificationSender : IPushNotificationSender
    {
        public List<SentNotification> SentNotifications { get; } = [];

        public Task<PushNotificationSendResult> SendAsync(PushNotificationPlatform platform, string deviceToken, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
        {
            SentNotifications.Add(new SentNotification(platform, deviceToken, title, body, new Dictionary<string, string>(data)));
            return Task.FromResult(new PushNotificationSendResult(true));
        }
    }

    private sealed record SentNotification(
        PushNotificationPlatform Platform,
        string DeviceToken,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string> Data);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FakeTimeProvider(DateTimeOffset now) => this.now = now;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
