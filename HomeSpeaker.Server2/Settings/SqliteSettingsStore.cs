using Microsoft.Data.Sqlite;

namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// Key/value settings kept in the app's SQLite database. The table is owned by this class rather than
/// the EF model, so it needs no migration. Keys use the configuration ':' form (<c>Immich:BaseUrl</c>).
/// </summary>
public sealed class SqliteSettingsStore(string connectionString)
{
    private const string CreateTableSql = "CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)";

    public IReadOnlyDictionary<string, string> LoadAll()
    {
        using var connection = open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSettings";
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    /// <summary>Upserts every change in one transaction; a null value deletes the key.</summary>
    public void Save(IReadOnlyDictionary<string, string?> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        using var connection = open();
        using var transaction = connection.BeginTransaction();
        foreach (var (key, value) in changes)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.Parameters.AddWithValue("$key", key);
            if (value is null)
            {
                command.CommandText = "DELETE FROM AppSettings WHERE Key = $key";
            }
            else
            {
                command.CommandText = "INSERT INTO AppSettings (Key, Value) VALUES ($key, $value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
                command.Parameters.AddWithValue("$value", value);
            }

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private SqliteConnection open()
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = CreateTableSql;
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
