using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class SensorReadingStoreTests
{
    private static readonly DateTimeOffset now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static SensorReadingStore create() =>
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new ManualTimeProvider(now));

    private static SensorReading reading(int i, TimeSpan old) => new()
    {
        Mac = $"C9:38:30:34:00:{i:X2}",
        Name = "GVH5100_00" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
        ObservedAt = now.UtcDateTime - old,
        Reporter = "kitchen",
    };

    [Fact]
    public void FullStore_RefusesNewSensors_ButUpdatesKnownOnes()
    {
        var store = create();
        for (var i = 0; i < SensorReadingStore.MaxSensors; i++)
        {
            Assert.True(store.Record(reading(i, TimeSpan.FromMinutes(1))));
        }

        Assert.False(store.Record(reading(200, TimeSpan.Zero)));
        Assert.True(store.Record(reading(0, TimeSpan.Zero)));
        Assert.Equal(SensorReadingStore.MaxSensors, store.All.Count);
    }

    [Fact]
    public void SensorsSilentForAWeek_AreForgotten()
    {
        var store = create();
        store.Record(reading(1, TimeSpan.FromDays(8)));
        store.Record(reading(2, TimeSpan.FromMinutes(1)));

        Assert.Equal(["C9:38:30:34:00:02"], store.All.Select(r => r.Mac));
    }
}
