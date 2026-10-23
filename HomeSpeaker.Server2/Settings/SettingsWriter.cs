namespace HomeSpeaker.Server2.Settings;

public sealed class SettingsWriter(SqliteSettingsStore? store, DatabaseSettingsConfigurationProvider? provider) : ISettingsWriter
{
    public void Save(IReadOnlyDictionary<string, string?> changes)
    {
        if (store is null || provider is null)
        {
            throw new InvalidOperationException("Database settings are not configured");
        }

        store.Save(changes);
        provider.Reload();
    }
}
