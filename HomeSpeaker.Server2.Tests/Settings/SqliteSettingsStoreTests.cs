using HomeSpeaker.Server2.Settings;
using HomeSpeaker.Server2.Tests.Photos;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SqliteSettingsStoreTests : IDisposable
{
    private readonly TempDirectory dir = new();

    private SqliteSettingsStore NewStore() => new($"Data Source={Path.Combine(dir.Path, "s.db")};Pooling=False");

    public void Dispose() => dir.Dispose();

    [Fact]
    public void LoadAll_NoDatabaseFile_ReturnsEmpty()
    {
        Assert.Empty(NewStore().LoadAll());
    }

    [Fact]
    public void Save_ThenLoadAll_RoundTrips()
    {
        var store = NewStore();
        store.Save(new Dictionary<string, string?> { ["Immich:BaseUrl"] = "http://x", ["A"] = "1" });
        store.Save(new Dictionary<string, string?> { ["A"] = "2" });

        var all = store.LoadAll();

        Assert.Equal(2, all.Count);
        Assert.Equal("http://x", all["Immich:BaseUrl"]);
        Assert.Equal("2", all["A"]);
    }

    [Fact]
    public void Save_NullValue_DeletesKey()
    {
        var store = NewStore();
        store.Save(new Dictionary<string, string?> { ["A"] = "1", ["B"] = "2" });
        store.Save(new Dictionary<string, string?> { ["A"] = null });

        var all = store.LoadAll();

        Assert.False(all.ContainsKey("A"));
        Assert.Equal("2", all["B"]);
    }
}
