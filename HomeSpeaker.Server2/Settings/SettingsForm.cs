using System.Globalization;
using Microsoft.Extensions.Configuration.Json;

namespace HomeSpeaker.Server2.Settings;

/// <summary>Turns configuration into the editable field list, and a submitted form into the database changes.</summary>
public static class SettingsForm
{
    /// <summary>Stands in for a secret that is set. Submitting it back means "leave the secret alone".</summary>
    public const string MaskedValue = "••••";

    public static IReadOnlyList<SettingField> Read(IConfiguration configuration, IReadOnlyDictionary<string, string> dbValues)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(dbValues);

        return [.. SettingKeys.All.Select(definition =>
        {
            var value = configuration[definition.Key];
            if (definition.IsSecret)
            {
                value = string.IsNullOrEmpty(value) ? null : MaskedValue;
            }

            return new SettingField(definition.Key, definition.Label, value, definition.IsSecret, IsOverridden(configuration, dbValues, definition.Key));
        })];
    }

    /// <summary>
    /// True when a source above the database (environment variables, command line) supplies the key, so the
    /// database value is ignored. Without a configuration root to inspect, falls back to comparing the effective
    /// value with the database value.
    /// </summary>
    public static bool IsOverridden(IConfiguration configuration, IReadOnlyDictionary<string, string> dbValues, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(dbValues);

        if (configuration is IConfigurationRoot root)
        {
            // Only sources above the database can override it; anything below is a default the database replaces.
            var providers = root.Providers.ToList();
            var databaseIndex = providers.FindIndex(p => p is DatabaseSettingsConfigurationProvider);
            for (var i = providers.Count - 1; i > databaseIndex; i--)
            {
                if (providers[i].TryGet(key, out _))
                {
                    return providers[i] is not (JsonConfigurationProvider or JsonStreamConfigurationProvider);
                }
            }

            return false;
        }

        return dbValues.TryGetValue(key, out var stored) && !string.Equals(stored, configuration[key], StringComparison.Ordinal);
    }

    /// <summary>
    /// The database writes for a submitted form: unknown and overridden keys are dropped, as are secrets sent back
    /// masked and values equal to the current one. An empty value becomes null, which deletes the key.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Changes(IReadOnlyList<SettingField> current, IReadOnlyDictionary<string, string?> submitted)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(submitted);

        var fields = current.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        var changes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in submitted)
        {
            if (!fields.TryGetValue(key, out var field) || field.IsOverridden)
            {
                continue;
            }

            if (field.IsSecret && string.Equals(raw, MaskedValue, StringComparison.Ordinal))
            {
                continue;
            }

            var value = field.IsSecret ? raw : raw?.Trim();
            value = string.IsNullOrEmpty(value) ? null : value;
            if (!field.IsSecret && string.Equals(value, string.IsNullOrEmpty(field.Value) ? null : field.Value, StringComparison.Ordinal))
            {
                continue;
            }

            if (field.IsSecret && value is null && field.Value is null)
            {
                continue;
            }

            changes[field.Key] = value;
        }

        return changes;
    }

    /// <summary>Error messages, by key, for submitted numbers that do not parse or are out of range. Blank is valid (it clears).</summary>
    public static IReadOnlyDictionary<string, string> Validate(IReadOnlyDictionary<string, string?> submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in submitted)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (SettingKeys.PositiveIntegers.Contains(key) &&
                (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) || whole < 1))
            {
                errors[key] = "Enter a whole number of 1 or more.";
            }
            else if (SettingKeys.Numbers.TryGetValue(key, out var range) &&
                (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                 number < range.Min || number > range.Max))
            {
                errors[key] = string.Create(CultureInfo.InvariantCulture, $"Enter a number from {range.Min} to {range.Max}.");
            }
        }

        return errors;
    }
}
