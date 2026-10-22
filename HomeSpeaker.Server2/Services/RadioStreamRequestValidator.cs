namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Checks the required fields of a radio stream create or update request. System.Text.Json does not
/// enforce required record parameters, so a body that omits name or url binds them as null.
/// </summary>
public static class RadioStreamRequestValidator
{
    /// <summary>Returns an error message, or null when the name is present and the URL is an http or https URL.</summary>
    public static string? Validate(string? name, string? url)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return "Url is required.";
        }

        if (StreamUrl.Normalize(url) is null)
        {
            return "Url must be an absolute http or https URL.";
        }

        return null;
    }
}
