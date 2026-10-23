using Microsoft.Data.Sqlite;

namespace HomeSpeaker.Server2.Settings;

public sealed class DatabaseSettingsConfigurationProvider(SqliteSettingsStore store) : ConfigurationProvider
{
    public override void Load()
    {
        // No logger exists this early; a missing or unreadable database just means no overrides.
        try
        {
            Data = new Dictionary<string, string?>(store.LoadAll()!, StringComparer.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Re-reads the database and fires the configuration change token.</summary>
    public void Reload()
    {
        Load();
        OnReload();
    }
}
