using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace HomeSpeaker.Server2.Settings;

public static class SettingsConfigurationExtensions
{
    /// <summary>
    /// Adds database-backed settings with the precedence appsettings JSON (including the environment file and
    /// user secrets) &lt; database &lt; unprefixed environment variables &lt; command line. The source goes right
    /// after the last JSON file source; with no JSON source, before the last unprefixed environment-variable source;
    /// otherwise at the end. (The host's ASPNETCORE_/DOTNET_ prefixed sources sit below the JSON files, so they are
    /// not a usable anchor.) A null or empty connection string adds nothing. Find the source afterwards with
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
        var sources = builder.Sources.ToList();
        var lastJson = sources.FindLastIndex(s => s is JsonConfigurationSource);
        var lastEnv = sources.FindLastIndex(s => s is EnvironmentVariablesConfigurationSource { Prefix: null or "" });
        if (lastJson >= 0)
        {
            builder.Sources.Insert(lastJson + 1, source);
        }
        else if (lastEnv >= 0)
        {
            builder.Sources.Insert(lastEnv, source);
        }
        else
        {
            builder.Sources.Add(source);
        }

        return builder;
    }
}
