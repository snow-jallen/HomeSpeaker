using HomeSpeaker.Server2.Settings;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SettingsFormTests : IDisposable
{
    private const string EnvPrefix = "HSTEST_SETTINGSFORM_";
    private readonly TempDirectory dir = new();
    private readonly List<string> envVars = [];

    private string ConnectionString => $"Data Source={Path.Combine(dir.Path, "s.db")};Pooling=False";

    public void Dispose()
    {
        foreach (var name in envVars)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        dir.Dispose();
    }

    private void setEnv(string key, string value)
    {
        var name = EnvPrefix + key.Replace(":", "__", StringComparison.Ordinal);
        Environment.SetEnvironmentVariable(name, value);
        envVars.Add(name);
    }

    /// <summary>appsettings (in memory) → database → environment variables, like the app.</summary>
    private IConfigurationRoot build(IDictionary<string, string?>? db = null, IDictionary<string, string?>? json = null)
    {
        if (db is not null)
        {
            new SqliteSettingsStore(ConnectionString).Save(db.ToDictionary(p => p.Key, p => p.Value));
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(json ?? new Dictionary<string, string?>())
            .AddDatabaseSettings(ConnectionString)
            .AddEnvironmentVariables(EnvPrefix)
            .Build();
    }

    private IReadOnlyDictionary<string, string> dbValues() => new SqliteSettingsStore(ConnectionString).LoadAll();

    private static SettingField field(IReadOnlyList<SettingField> fields, string key) =>
        Assert.Single(fields, f => string.Equals(f.Key, key, StringComparison.Ordinal));

    [Fact]
    public void Read_EnvOverriddenKey_IsOverriddenAndReadOnly()
    {
        setEnv(SettingKeys.ForecastLatitude, "41.5");
        var config = build(new Dictionary<string, string?> { [SettingKeys.ForecastLatitude] = "40.0", [SettingKeys.ForecastLongitude] = "-111.0" });

        var fields = SettingsForm.Read(config, dbValues());

        var latitude = field(fields, SettingKeys.ForecastLatitude);
        Assert.True(latitude.IsOverridden);
        Assert.Equal("41.5", latitude.Value);
        Assert.False(field(fields, SettingKeys.ForecastLongitude).IsOverridden);
        Assert.False(field(fields, SettingKeys.NightscoutUrl).IsOverridden);

        var changes = SettingsForm.Changes(fields, new Dictionary<string, string?> { [SettingKeys.ForecastLatitude] = "1" });
        Assert.Empty(changes);
    }

    [Fact]
    public void Read_AppsettingsDefault_IsNotOverridden()
    {
        var config = build(json: new Dictionary<string, string?> { [SettingKeys.TemperatureThreshold] = "2" });

        var threshold = field(SettingsForm.Read(config, dbValues()), SettingKeys.TemperatureThreshold);

        Assert.False(threshold.IsOverridden);
        Assert.Equal("2", threshold.Value);
    }

    [Fact]
    public void Read_SecretValue_IsMasked()
    {
        var config = build(new Dictionary<string, string?> { [SettingKeys.ImmichApiKey] = "super-secret" });

        var fields = SettingsForm.Read(config, dbValues());

        var key = field(fields, SettingKeys.ImmichApiKey);
        Assert.True(key.IsSecret);
        Assert.Equal(SettingsForm.MaskedValue, key.Value);
        Assert.Equal("••••", SettingsForm.MaskedValue);
        Assert.Null(field(fields, SettingKeys.TemperatureApiKey).Value);
        Assert.DoesNotContain(fields, f => f.Value == "super-secret");
    }

    [Fact]
    public void Changes_DropsOverriddenAndUnknownKeys()
    {
        IReadOnlyList<SettingField> current =
        [
            new(SettingKeys.ForecastLatitude, "Latitude", "40", IsSecret: false, IsOverridden: true),
            new(SettingKeys.ForecastLongitude, "Longitude", null, IsSecret: false, IsOverridden: false),
        ];

        var changes = SettingsForm.Changes(current, new Dictionary<string, string?>
        {
            [SettingKeys.ForecastLatitude] = "41",
            [SettingKeys.ForecastLongitude] = "-111.5",
            ["Admin:PinHash"] = "evil",
            ["Nope:Unknown"] = "x",
        });

        Assert.Equal(new Dictionary<string, string?> { [SettingKeys.ForecastLongitude] = "-111.5" }, changes);
    }

    [Fact]
    public void Changes_MaskedSecretUnchanged_NotWritten()
    {
        IReadOnlyList<SettingField> current =
        [
            new(SettingKeys.ImmichApiKey, "Immich API key", SettingsForm.MaskedValue, IsSecret: true, IsOverridden: false),
            new(SettingKeys.TemperatureApiKey, "Govee API key", SettingsForm.MaskedValue, IsSecret: true, IsOverridden: false),
        ];

        var changes = SettingsForm.Changes(current, new Dictionary<string, string?>
        {
            [SettingKeys.ImmichApiKey] = "••••",
            [SettingKeys.TemperatureApiKey] = "new-key",
        });

        Assert.Equal(new Dictionary<string, string?> { [SettingKeys.TemperatureApiKey] = "new-key" }, changes);
    }

    [Fact]
    public void Changes_EmptyString_DeletesKey()
    {
        IReadOnlyList<SettingField> current =
        [
            new(SettingKeys.NightscoutUrl, "Nightscout URL", "https://ns.example", IsSecret: false, IsOverridden: false),
            new(SettingKeys.ImmichApiKey, "Immich API key", SettingsForm.MaskedValue, IsSecret: true, IsOverridden: false),
        ];

        var changes = SettingsForm.Changes(current, new Dictionary<string, string?>
        {
            [SettingKeys.NightscoutUrl] = "",
            [SettingKeys.ImmichApiKey] = "",
        });

        Assert.Equal(2, changes.Count);
        Assert.Null(changes[SettingKeys.NightscoutUrl]);
        Assert.Null(changes[SettingKeys.ImmichApiKey]);
    }

    [Fact]
    public void Changes_UnchangedValue_NotWritten()
    {
        IReadOnlyList<SettingField> current =
        [
            new(SettingKeys.OpenAiChatModel, "Chat model", "gpt-4o-mini", IsSecret: false, IsOverridden: false),
        ];

        var changes = SettingsForm.Changes(current, new Dictionary<string, string?> { [SettingKeys.OpenAiChatModel] = "gpt-4o-mini" });

        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(SettingKeys.SlideSeconds, "abc", false)]
    [InlineData(SettingKeys.SlideSeconds, "12.5", false)]
    [InlineData(SettingKeys.SlideSeconds, "0", false)]
    [InlineData(SettingKeys.SlideSeconds, "20", true)]
    [InlineData(SettingKeys.IdleSeconds, "", true)]
    [InlineData(SettingKeys.ForecastLatitude, "91", false)]
    [InlineData(SettingKeys.ForecastLatitude, "39.36", true)]
    [InlineData(SettingKeys.ForecastLongitude, "-181", false)]
    [InlineData(SettingKeys.TemperatureThreshold, "2.5", true)]
    [InlineData(SettingKeys.NightscoutUrl, "anything", true)]
    public void Validate_NumericFields(string key, string value, bool valid)
    {
        var errors = SettingsForm.Validate(new Dictionary<string, string?> { [key] = value });

        Assert.Equal(valid, !errors.ContainsKey(key));
    }
}
