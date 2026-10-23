namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// Configuration source backed by <see cref="SqliteSettingsStore"/>. Builds exactly one provider and
/// keeps it in <see cref="Provider"/> so DI can hand the same instance to <see cref="SettingsWriter"/>.
/// </summary>
public sealed class DatabaseSettingsConfigurationSource(SqliteSettingsStore store) : IConfigurationSource
{
    public SqliteSettingsStore Store { get; } = store;

    /// <summary>The provider created by <see cref="Build"/>; null until the configuration is built.</summary>
    public DatabaseSettingsConfigurationProvider? Provider { get; private set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        Provider ??= new DatabaseSettingsConfigurationProvider(Store);
}
