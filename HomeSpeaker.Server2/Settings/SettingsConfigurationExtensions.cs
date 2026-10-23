using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace HomeSpeaker.Server2.Settings;

public static class SettingsConfigurationExtensions
{
    /// <summary>
    /// Adds database-backed settings just before the first environment-variable source, giving the
    /// precedence appsettings.json, database, environment variables, command line. A null or empty
    /// connection string adds nothing. Find the source afterwards with
    /// <c>Sources.OfType&lt;DatabaseSettingsConfigurationSource&gt;()</c> to reach its store and provider.
    /// </summary>
    public static IConfigurationBuilder AddDatabaseSettings(this IConfigurationBuilder builder, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return builder;
        }

        var source = new DatabaseSettingsConfigurationSource(new SqliteSettingsStore(connectionString));
        var index = builder.Sources.ToList().FindIndex(s => s is EnvironmentVariablesConfigurationSource);
        if (index < 0)
        {
            builder.Sources.Add(source);
        }
        else
        {
            builder.Sources.Insert(index, source);
        }

        return builder;
    }
}
